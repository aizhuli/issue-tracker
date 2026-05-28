export type IssueStatus = "backlog" | "todo" | "in-progress" | "in-review" | "done";
export type IssuePriority = "low" | "medium" | "high" | "urgent";

export type UserSummary = {
  id: string;
  name: string;
  email: string;
  avatarUrl?: string | null;
};

export type LabelDto = {
  id: string;
  name: string;
  color: string;
};

export type IssueFull = {
  id: string;
  number: number;
  displayKey: string;
  title: string;
  description?: string | null;
  status: IssueStatus;
  priority: IssuePriority;
  assignee: UserSummary | null;
  reporter: UserSummary;
  labels: LabelDto[];
  acceptanceCriteria?: string | null;
  commentCount: number;
  createdAt: string;
  updatedAt: string;
  closedAt?: string | null;
};

export type IssueSummary = {
  id: string;
  number: number;
  displayKey: string;
  title: string;
  status: IssueStatus;
  priority: IssuePriority;
  assignee: UserSummary | null;
  labels: LabelDto[];
  commentCount: number;
  updatedAt: string;
};

export type CommentDto = {
  id: string;
  authorId: string;
  authorName: string;
  authorAvatarUrl?: string | null;
  body: string;
  createdAt: string;
  updatedAt: string;
  edited: boolean;
};

export type LabelFullDto = {
  id: string;
  name: string;
  color: string;
  createdAt: string;
};

export type TriageSuggestion = {
  priority: IssuePriority;
  labels: LabelDto[];
  acceptanceCriteria: string;
};
