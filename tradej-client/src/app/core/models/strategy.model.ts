export interface StrategyListItem {
  id: number;
  name: string;
  description?: string;
  imageUrl?: string;
  tradeCount: number;
  winRate: number;
  netPnL: number;
}

export interface StrategyStats {
  totalTrades: number;
  winners: number;
  losers: number;
  netPnL: number;
  winRate: number;
  profitFactor: number;
  avgWin: number;
  avgLoss: number;
  avgHoldMinutes: number;
}

export interface StrategyTrade {
  id: number;
  symbol: string;
  direction: string;
  entryTime: string;
  exitTime?: string;
  netPnL: number;
  volume: number;
}

export interface StrategyNote {
  id: number;
  title: string;
  content: string;
  createdAt: string;
  updatedAt: string;
}

export interface StrategyDetail {
  id: number;
  accountId: number;
  name: string;
  description?: string;
  imageUrl?: string;
  createdAt: string;
  updatedAt: string;
  stats: StrategyStats;
  trades: StrategyTrade[];
  notes: StrategyNote[];
}

export interface CreateStrategyDto {
  name: string;
  description?: string;
}

export interface UpdateStrategyDto {
  name: string;
  description?: string;
}
