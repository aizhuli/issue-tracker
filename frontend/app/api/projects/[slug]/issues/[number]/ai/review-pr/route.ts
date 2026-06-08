import { NextRequest } from "next/server";
import { passthrough, serverFetch } from "@/lib/api-client";
import { getSession } from "@/lib/session";
import { MISSING_SESSION_RESPONSE } from "@/lib/bff-responses";

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ slug: string; number: string }> },
) {
  const session = await getSession();
  if (!session.user) {
    return Response.json(MISSING_SESSION_RESPONSE, {
      status: 401,
      headers: { "content-type": "application/problem+json" },
    });
  }

  const { slug, number } = await params;
  const body = await request.text();

  const upstream = await serverFetch(
    `/api/projects/${slug}/issues/${number}/ai/review-pr`,
    {
      method: "POST",
      body,
      user: session.user,
    },
  );

  return passthrough(upstream);
}
