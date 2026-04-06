export interface DayStats {
  totalTrades: number;
  winners: number;
  losers: number;
  grossPnL: number;
  netPnL: number;
  commission: number;
  swap: number;
  volume: number;
  winRate: number;
  profitFactor: number;
}

export interface DayTradeItem {
  id: number;
  symbol: string;
  direction: 'Long' | 'Short';
  status: string;
  entryTime: string;
  exitTime?: string;
  entryPrice: number;
  exitPrice?: number;
  volume: number;
  grossPnL: number;
  commission: number;
  swap: number;
  netPnL: number;
  tags?: string;
  durationMinutes: number;
}

export interface DayGroup {
  date: string;
  dayLabel: string;
  weekNumber: number;
  stats: DayStats;
  trades: DayTradeItem[];
  note?: string;
  tagIds: number[];
}

export interface DayViewData {
  days: DayGroup[];
}

export interface WeekGroup {
  weekNumber: number;
  weekLabel: string;
  days: DayGroup[];
  stats: DayStats;
}

export interface DayNote {
  id: number;
  date: string;
  content: string;
  updatedAt: string;
  tagIds: number[];
}

export interface DayNotePage {
  total: number;
  page: number;
  pageSize: number;
  items: DayNote[];
}

export interface DayTagDef {
  id: number;
  name: string;
  color: string;
}

