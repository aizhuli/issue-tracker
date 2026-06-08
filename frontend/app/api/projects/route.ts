import { NextRequest } from "next/server";
import { passthrough, serverFetch } from "@/lib/api-client";
import { getSession } from "@/lib/session";
import { MISSING_SESSION_RESPONSE } from "@/lib/bff-responses";

export async function GET(request: NextRequest) {
  const session = await getSession();
  if (!session.user) {
    return Response.json(MISSING_SESSION_RESPONSE, {
      status: 401,
      headers: { "content-type": "application/problem+json" },
    });
  }

  const url = new URL(request.url);
  const pathWithQuery = `/api/projects${url.search}`;

  const upstream = await serverFetch(pathWithQuery, {
    method: "GET",
    user: session.user,
  });

  return passthrough(upstream);
}

export async function POST(request: NextRequest) {
  const session = await getSession();
  if (!session.user) {
    return Response.json(MISSING_SESSION_RESPONSE, {
      status: 401,
      headers: { "content-type": "application/problem+json" },
    });
  }

  const body = await request.text();

  const upstream = await serverFetch("/api/projects", {
    method: "POST",
    body,
    user: session.user,
  });

  return passthrough(upstream);
}
