import { notFound, redirect } from "next/navigation";
import { getSession } from "@/lib/session";
import { serverFetch } from "@/lib/api-client";
import type { IssueFull } from "@/lib/types/issues";
import { IssueDetailClient } from "./_components/IssueDetailClient";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type ProjectInfo = {
  id: string;
  slug: string;
  name: string;
  ownerId: string;
  ownerName: string;
};

type MeInfo = {
  id: string;
  name: string;
  email: string;
};

// ---------------------------------------------------------------------------
// Page (Server Component)
// ---------------------------------------------------------------------------

export default async function IssueDetailPage({
  params,
}: {
  params: Promise<{ slug: string; number: string }>;
}) {
  const { slug, number } = await params;

  // Reject non-numeric issue number before hitting the backend
  if (!/^\d+$/.test(number)) notFound();

  const session = await getSession();
  if (!session.user) {
    redirect("/login");
  }

  // Fetch me, project, and issue in parallel — project and issue are independent
  const [meRes, projectRes, issueRes] = await Promise.all([
    serverFetch("/api/auth/me", { method: "GET", user: session.user }),
    serverFetch(`/api/projects/${slug}`, { method: "GET", user: session.user }),
    serverFetch(`/api/projects/${slug}/issues/${number}`, {
      method: "GET",
      user: session.user,
    }),
  ]);

  // Any auth failure → treat as session expired
  if (!meRes.ok) redirect("/login");

  // Any project or issue failure (404, 500, etc.) → not found rather than broken page
  if (!projectRes.ok) notFound();
  if (!issueRes.ok) notFound();

  const me: MeInfo = await meRes.json();
  const project: ProjectInfo = await projectRes.json();
  const issue: IssueFull = await issueRes.json();

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        height: "100%",
        overflow: "hidden",
      }}
    >
      {/* Back nav bar */}
      <div
        style={{
          height: "var(--viewbar-h, 44px)",
          borderBottom: "1px solid var(--border)",
          display: "flex",
          alignItems: "center",
          padding: "0 12px",
          background: "var(--surface)",
          flexShrink: 0,
          gap: 10,
        }}
      >
        <a
          href={`/projects/${slug}`}
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: 5,
            fontSize: 12.5,
            fontWeight: 500,
            color: "var(--ink-2)",
            textDecoration: "none",
            padding: "4px 8px",
            borderRadius: "var(--radius-sm)",
            border: "1px solid var(--border)",
            background: "var(--surface)",
            transition: "border-color 80ms ease, color 80ms ease",
          }}
        >
          ← {project.name}
        </a>

        <span
          style={{
            fontFamily: "var(--font-jetbrains-mono), monospace",
            fontSize: 11,
            fontWeight: 500,
            color: "var(--ink-3)",
            letterSpacing: "0.04em",
          }}
        >
          {issue.displayKey}
        </span>
      </div>

      {/* Issue detail — client wrapper provides onChange / onDeleted */}
      <div style={{ flex: 1, overflow: "hidden" }}>
        <IssueDetailClient
          initialIssue={issue}
          me={me}
          projectOwnerId={project.ownerId}
          projectSlug={slug}
        />
      </div>
    </div>
  );
}
