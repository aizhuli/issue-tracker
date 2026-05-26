import { NextRequest } from "next/server";
import { passthrough, serverFetch } from "@/lib/api-client";
import { getSession } from "@/lib/session";

const MISSING_SESSION_RESPONSE = {
  type: "https://tools.ietf.org/html/rfc7807",
  title: "Unauthorized",
  status: 401,
  detail: "No active session.",
  errorCode: "auth:session:missing",
};

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ slug: string; number: string; commentId: string }> },
) {
  const session = await getSession();
  if (!session.user) {
    return Response.json(MISSING_SESSION_RESPONSE, {
      status: 401,
      headers: { "content-type": "application/problem+json" },
    });
  }

  const { slug, number, commentId } = await params;
  const body = await request.text();

  const upstream = await serverFetch(
    `/api/projects/${slug}/issues/${number}/comments/${commentId}`,
    {
      method: "PUT",
      body,
      user: session.user,
    },
  );

  return passthrough(upstream);
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ slug: string; number: string; commentId: string }> },
) {
  const session = await getSession();
  if (!session.user) {
    return Response.json(MISSING_SESSION_RESPONSE, {
      status: 401,
      headers: { "content-type": "application/problem+json" },
    });
  }

  const { slug, number, commentId } = await params;

  const upstream = await serverFetch(
    `/api/projects/${slug}/issues/${number}/comments/${commentId}`,
    {
      method: "DELETE",
      user: session.user,
    },
  );

  return passthrough(upstream);
}
