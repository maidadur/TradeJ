export interface Tag {
  id: number;
  categoryId: number;
  name: string;
  usageCount: number;
}

export interface TagCategory {
  id: number;
  accountId: number;
  name: string;
  color: string;
  sortOrder: number;
  tags: Tag[];
}
