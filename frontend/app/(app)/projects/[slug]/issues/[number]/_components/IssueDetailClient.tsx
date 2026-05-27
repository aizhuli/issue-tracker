"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { IssueDetail } from "@/components/issues/IssueDetail";
import type { IssueFull } from "@/lib/types/issues";

interface IssueDetailClientProps {
  initialIssue: IssueFull;
  me: { id: string; name: string; email: string };
  projectOwnerId: string;
  projectSlug: string;
}

export function IssueDetailClient({
  initialIssue,
  me,
  projectOwnerId,
  projectSlug,
}: IssueDetailClientProps) {
  const router = useRouter();
  const [issue, setIssue] = useState<IssueFull>(initialIssue);

  function handleChange(next: IssueFull) {
    setIssue(next);
  }

  function handleDeleted() {
    router.push(`/projects/${projectSlug}`);
  }

  return (
    <IssueDetail
      issue={issue}
      me={me}
      projectOwnerId={projectOwnerId}
      projectSlug={projectSlug}
      onChange={handleChange}
      onDeleted={handleDeleted}
    />
  );
}
