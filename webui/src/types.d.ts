import { modelsv2 } from "algosdk";

export interface NodeStatus {
  serviceStatus: string;
  port: number;
  token: string;
  p2p: boolean;
  retiStatus?: RetiStatus;
}

export interface RetiStatus {
  serviceStatus: string;
  version?: string;
  exeStatus?: string;
}

export interface SnackBar {
  text: string;
  color: string;
  timeout: number;
  display: boolean;
}

export interface Participation {
  address: string;
  id: string;
  key: modelsv2.AccountParticipation;
}

export interface Peer {
  address: string;
  network: string;
  outgoing: boolean;
}

export interface GoalVersion {
  installed: string;
  latest: string;
}
