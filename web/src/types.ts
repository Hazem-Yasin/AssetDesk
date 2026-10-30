export type Role = 'Employee' | 'Admin';
export type AssetStatus = 'Available' | 'CheckedOut' | 'InMaintenance' | 'Retired';
export type AssetCategory = 'Laptop' | 'Desktop' | 'Monitor' | 'Phone' | 'Peripheral' | 'Other';

export const STATUSES: AssetStatus[] = ['Available', 'CheckedOut', 'InMaintenance', 'Retired'];
export const CATEGORIES: AssetCategory[] = ['Laptop', 'Desktop', 'Monitor', 'Phone', 'Peripheral', 'Other'];

export interface User {
  id: number;
  email: string;
  displayName: string;
  role: Role;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface Assignment {
  id: number;
  assetId: number;
  assetTag: string;
  assetName: string;
  userId: number;
  userName: string;
  checkedOutAt: string;
  dueAt: string;
  returnedAt: string | null;
  isOverdue: boolean;
  notes: string | null;
}

export interface Asset {
  id: number;
  tag: string;
  name: string;
  category: AssetCategory;
  serialNumber: string | null;
  purchaseDate: string;
  purchasePrice: number;
  notes: string | null;
  status: AssetStatus;
  version: number;
  currentAssignment: Assignment | null;
}

export interface Attachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface AssetDetail {
  asset: Asset;
  history: Assignment[];
  attachments: Attachment[];
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Stats {
  totalAssets: number;
  overdue: number;
  totalValue: number;
  byStatus: Record<string, number>;
  byCategory: Record<string, number>;
}

export interface AssetInput {
  tag: string;
  name: string;
  category: AssetCategory;
  serialNumber: string | null;
  purchaseDate: string;
  purchasePrice: number;
  notes: string | null;
}
