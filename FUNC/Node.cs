﻿using System.Formats.Tar;
using FUNC.Models;
using Newtonsoft.Json.Linq;
using static System.OperatingSystem;

namespace FUNC
{
    public class Node
    {
        private static async Task ExtractTemplate(string name)
        {
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", $"{name}.tar");
            await TarFile.ExtractToDirectoryAsync(templatePath, Utils.NodeDataParent(name), true);
        }

        public static async Task<NodeStatus> Get(string name)
        {
            string machineName = Environment.MachineName;
            int port = 0;
            string token = string.Empty;
            try { token = File.ReadAllText(Path.Combine(Utils.NodeDataParent(name), name, "algod.admin.token")); } catch { }
            string? configText = null;
            try { configText = File.ReadAllText(Path.Combine(Utils.NodeDataParent(name), name, "config.json")); } catch { }
            if (configText != null)
            {
                JObject config = JObject.Parse(configText);
                var endpointAddressToken = config.GetValue("EndpointAddress");
                string endpointAddress = endpointAddressToken?.Value<string>() ?? ":0";
                port = int.Parse(endpointAddress[(endpointAddress.IndexOf(":") + 1)..]);
                if (name == "algorand")
                    Shared.AlgoPort = port;
                else if (name == "voi")
                    Shared.VoiPort = port;
            }

            string sc = string.Empty;

            if (IsWindows())
            {
                sc = await Utils.ExecCmd($"sc query \"{Utils.Cap(name)} Node\"");
            }
            else if (IsLinux())
            {
                sc = await Utils.ExecCmd($"systemctl show {name} --property=LoadState --property=ActiveState");
            }
            else if (IsMacOS())
            {
                sc = await Utils.ExecCmd($"launchctl list | grep -i func.{name} || echo none");
            }

            string serviceStatus = Utils.ParseServiceStatus(sc);

            NodeStatus nodeStatus = new()
            {
                MachineName = machineName,
                ServiceStatus = serviceStatus,
                Port = port,
                Token = token,
            };

            if (name == "algorand")
            {
                // Reti Status
                string retiQuery = string.Empty;
                string exePath = Path.Combine(Utils.appDataDir, "reti", "reti");

                if (IsWindows())
                {
                    retiQuery = await Utils.ExecCmd("sc query \"Reti Validator\"");
                    exePath += ".exe";
                }
                else if (IsLinux())
                {
                    retiQuery = await Utils.ExecCmd($"systemctl show reti --property=LoadState --property=ActiveState");
                }
                else if (IsMacOS())
                {
                    retiQuery = await Utils.ExecCmd($"launchctl list | grep -i func.reti || echo none");
                }

                string retiServiceStatus = Utils.ParseServiceStatus(retiQuery);

                string? version = null;
                if (File.Exists(exePath))
                {
                    version = await Utils.ExecCmd(exePath + " --version");
                }

                string? exeStatus = null;
                if (retiServiceStatus == "Running")
                {
                    try
                    {
                        using HttpClient client = new();
                        var ready = await client.GetAsync("http://localhost:6260/ready");
                        exeStatus = ready.IsSuccessStatusCode ? "Running" : "Stopped";
                    }
                    catch
                    {
                        exeStatus = "Stopped";
                    }
                }

                RetiStatus retiStatus = new()
                {
                    ServiceStatus = retiServiceStatus,
                    Version = version,
                    ExeStatus = exeStatus,
                };

                nodeStatus.RetiStatus = retiStatus;

                // Telemetry Status
                string diagcfgPath = Path.Combine(Utils.appDataDir, "bin", "diagcfg");
                string dataPath = Path.Combine(Utils.NodeDataParent(name), name);
                string telemetryStatus = await Utils.ExecCmd($"{diagcfgPath} -d {dataPath} telemetry status");

                nodeStatus.TelemetryStatus = telemetryStatus;
            }

            return nodeStatus;
        }

        public static async Task CreateService(string name)
        {
            if (!Directory.Exists(Path.Combine(Utils.NodeDataParent(name), name)))
            {
                await ExtractTemplate(name);
            }

            if (IsWindows())
            {
                string nodeDataDir = Path.Combine(Utils.NodeDataParent(name), name);
                string binPath = $"\\\"{Path.Combine(AppContext.BaseDirectory, "Services", "NodeServiceV2.exe")}\\\" \\\"{nodeDataDir}\\\"";
                await Utils.ExecCmd($"sc create \"{Utils.Cap(name)} Node\" binPath= \"{binPath}\" start= auto");
            }
            else if (IsLinux())
            {
                string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "node.service");
                string template = File.ReadAllText(templatePath);
                string service = template.Replace("__NAME__", name).Replace("__PARENTDIR__", Utils.NodeDataParent(name));
                File.WriteAllText($"/lib/systemd/system/{name}.service", service);
                await Utils.ExecCmd($"systemctl daemon-reload");
                await Utils.ExecCmd($"systemctl enable {name}");
            }
            else if (IsMacOS())
            {
                string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "func.node.plist");
                string template = File.ReadAllText(templatePath);
                string plist = template.Replace("__NAME__", name).Replace("__PARENTDIR__", Utils.NodeDataParent(name));
                File.WriteAllText($"/Library/LaunchDaemons/func.{name}.plist", plist);
                await Utils.ExecCmd($"launchctl bootstrap system /Library/LaunchDaemons/func.{name}.plist");
            }
        }

        public static async Task ResetData(string name)
        {
            if (Directory.Exists(Path.Combine(Utils.NodeDataParent(name), name)))
            {
                Directory.Delete(Path.Combine(Utils.NodeDataParent(name), name), true);
            }
            await ExtractTemplate(name);
        }

        public static async Task<string> Catchup(string name, Catchup model)
        {
            string goalPath = Path.Combine(Utils.appDataDir, "bin", "goal");
            string dataPath = Path.Combine(Utils.NodeDataParent(name), name);
            string cmd = $"{goalPath} node catchup {model.Round}#{model.Label} -d {dataPath}";
            return await Utils.ExecCmd(cmd);
        }

        public static async Task ControlService(string name, string cmd)
        {
            if (IsWindows())
            {
                if (cmd == "restart")
                {
                    await ControlService(name, "stop");
                    await ControlService(name, "start");
                }
                await Utils.ExecCmd($"sc {cmd} \"{Utils.Cap(name)} Node\"");
            }
            else if (IsLinux())
            {
                if (cmd == "delete") await Utils.ExecCmd($"rm /lib/systemd/system/{name}.service");
                else await Utils.ExecCmd($"systemctl {cmd} {name}");
                await Utils.ExecCmd($"systemctl daemon-reload");
            }
            else if (IsMacOS())
            {
                if (cmd == "start") await Utils.ExecCmd($"launchctl kickstart system/func.{name}");
                else if (cmd == "stop") await Utils.ExecCmd($"launchctl kill 9 system/func.{name}");
                else if (cmd == "restart") await Utils.ExecCmd($"launchctl kickstart -k system/func.{name}");
                else if (cmd == "delete")
                {
                    await Utils.ExecCmd($"launchctl bootout system/func.{name}");
                    await Utils.ExecCmd($"rm /Library/LaunchDaemons/func.{name}.plist");
                }
            }
        }

        public static async Task<string> GetConfig(string name)
        {
            if (!Directory.Exists(Path.Combine(Utils.NodeDataParent(name), name)))
            {
                await ExtractTemplate(name);
            }
            string configPath = Path.Combine(Utils.NodeDataParent(name), name, "config.json");
            string config = File.ReadAllText(configPath);
            return config;
        }

        public static void SetConfig(string name, Config model)
        {
            string configPath = Path.Combine(Utils.NodeDataParent(name), name, "config.json");
            File.WriteAllText(configPath, model.Json);
        }

        public static void SetDir(string name, Dir model)
        {
            string currentPath = Path.Combine(Utils.NodeDataParent(name), name);
            string requestPath = Path.Combine(model.Path, name);
            CopyFolder(currentPath, requestPath);
            string filePath = Path.Combine(Utils.appDataDir, $"{name}.data");
            File.WriteAllText(filePath, model.Path);
            Directory.Delete(currentPath, true);
        }

        public static void CopyFolder(string sourceFolder, string destFolder)
        {
            Directory.CreateDirectory(destFolder);
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(destFolder, name);
                File.Copy(file, dest);
            }
            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyFolder(folder, dest);
            }
        }

        public static async Task EnableTelemetry(string name)
        {
            string diagcfgPath = Path.Combine(Utils.appDataDir, "bin", "diagcfg");
            string dataPath = Path.Combine(Utils.NodeDataParent(name), name);
            await Utils.ExecCmd($"{diagcfgPath} -d {dataPath} telemetry endpoint -e https://tel.4160.nodely.io");
            await Utils.ExecCmd($"{diagcfgPath} -d {dataPath} telemetry name -n anon");
            await Utils.ExecCmd($"{diagcfgPath} -d {dataPath} telemetry enable");
            await ControlService(name, "restart");
        }

        public static async Task DisableTelemetry(string name)
        {
            string diagcfgPath = Path.Combine(Utils.appDataDir, "bin", "diagcfg");
            string dataPath = Path.Combine(Utils.NodeDataParent(name), name);
            await Utils.ExecCmd($"{diagcfgPath} -d {dataPath} telemetry disable");
            await ControlService(name, "restart");
        }
    }
}
