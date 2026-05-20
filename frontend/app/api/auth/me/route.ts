import { passthrough, serverFetch } from "@/lib/api-client";
import { getSession } from "@/lib/session";

export async function GET() {
  const session = await getSession();
  if (!session.user) {
    return Response.json(
      {
        type: "https://tools.ietf.org/html/rfc7807",
        title: "Unauthorized",
        status: 401,
        detail: "No active session.",
        errorCode: "auth:session:missing",
      },
      { status: 401, headers: { "content-type": "application/problem+json" } },
    );
  }

  const upstream = await serverFetch("/api/auth/me", {
    method: "GET",
    user: session.user,
  });

  return passthrough(upstream);
}
