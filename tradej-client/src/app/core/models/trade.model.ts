export interface Trade {
  id: number;
  accountId: number;
  accountName: string;
  brokerTradeId: string;
  symbol: string;
  direction: 'Long' | 'Short';
  status: 'Open' | 'Closed' | 'Cancelled';
  entryPrice: number;
  exitPrice?: number;
  entryTime: string;
  exitTime?: string;
  volume: number;
  grossPnL: number;
  commission: number;
  swap: number;
  netPnL: number;
  notes?: string;
  tags?: string;
  importedAt: string;
  tagIds: number[];
  strategyIds: number[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface TradeFilter {
  accountId: number;
  page?: number;
  pageSize?: number;
  symbol?: string;
  direction?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
  sortBy?: string;
  sortDesc?: boolean;
}
