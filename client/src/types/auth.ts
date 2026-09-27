export type UserRole = 
  | 'Customer' 
  | 'VendorOwner' 
  | 'Moderator' 
  | 'Finance' 
  | 'CustomerCare' 
  | 'SuperAdmin';

export interface User {
  id: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  role: UserRole;
  avatarUrl?: string;
  vendorId?: string;
  vendorBrandName?: string;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterCustomerRequest {
  fullName: string;
  email: string;
  phoneNumber: string;
  password: string;
}

export interface RegisterVendorRequest {
  fullName: string;
  email: string;
  phoneNumber: string;
  password: string;
  brandName: string;
  city: string;
  commissionRate: number;
}
