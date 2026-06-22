export interface UserAdminItem {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
}

export interface TopVideo { id: string; title: string; views: number; }

export interface StatsSummary {
  totalVideos: number;
  videosByStatus: Record<string, number>;
  totalUsers: number;
  totalViews: number;
  totalWatchSeconds: number;
  topVideos: TopVideo[];
}

export interface AuditEntry {
  id: string;
  actorId: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  occurredAt: string;
}
