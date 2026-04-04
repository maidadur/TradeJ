export interface DashboardSummary {
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  breakEvenTrades: number;
  winRate: number;
  totalNetPnL: number;
  totalGrossPnL: number;
  totalCommission: number;
  totalSwap: number;
  averageWin: number;
  averageLoss: number;
  profitFactor: number;
  maxDrawdown: number;
  largestWin: number;
  largestLoss: number;
  averageHoldingTimeMinutes: number;
}

export interface MonthlyStats {
  year: number;
  month: number;
  monthName: string;
  tradeCount: number;
  winCount: number;
  lossCount: number;
  netPnL: number;
  winRate: number;
}

export interface DailyStats {
  date: string;
  tradeCount: number;
  winCount: number;
  lossCount: number;
  netPnL: number;
}

export interface SymbolStats {
  symbol: string;
  tradeCount: number;
  winCount: number;
  lossCount: number;
  netPnL: number;
  winRate: number;
}

export interface Dashboard {
  summary: DashboardSummary;
  monthlyStats: MonthlyStats[];
  dailyStats: DailyStats[];
  symbolStats: SymbolStats[];
}
