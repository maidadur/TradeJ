export interface Account {
  id: number;
  name: string;
  broker: string;
  accountNumber: string;
  currency: string;
  initialBalance: number;
  isActive: boolean;
  createdAt: string;
  tradeCount: number;
  mt5Server?: string | null;
  hasMT5InvestorPassword?: boolean;
  metaApiAccountId?: string | null;
  hasMetaApiToken?: boolean;
  metaApiRegion: string;
}

export interface CreateAccountDto {
  name: string;
  broker: string;
  accountNumber: string;
  currency: string;
  initialBalance?: number;
  mt5Server?: string | null;
  mt5InvestorPassword?: string | null;
  metaApiAccountId?: string | null;
  metaApiToken?: string | null;
  metaApiRegion?: string;
}

export interface UpdateAccountDto {
  name: string;
  accountNumber: string;
  currency: string;
  initialBalance: number;
  isActive: boolean;
  mt5Server?: string | null;
  mt5InvestorPassword?: string | null;
  metaApiAccountId?: string | null;
  metaApiToken?: string | null;
  metaApiRegion?: string;
}
