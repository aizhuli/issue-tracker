import { NextResponse, type NextRequest } from "next/server";
import { getIronSession } from "iron-session";
import type { SessionData } from "./lib/session";

const PUBLIC_PATHS = ["/login", "/register"];

export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  if (PUBLIC_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`))) {
    return NextResponse.next();
  }

  const sessionPassword = process.env.SESSION_COOKIE_PASSWORD;
  if (!sessionPassword || sessionPassword.length < 32) {
    return NextResponse.next();
  }

  const response = NextResponse.next();
  const session = await getIronSession<SessionData>(request, response, {
    cookieName: "ait_session",
    password: sessionPassword,
  });

  if (!session.user) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("returnTo", pathname);
    return NextResponse.redirect(loginUrl);
  }

  return response;
}

export const config = {
  matcher: [
    /*
     * Match all request paths except:
     * - /api/auth/* (auth route handlers manage their own access)
     * - /_next/* (Next.js internals)
     * - static files (favicon, images, etc.)
     */
    "/((?!api/auth|_next/static|_next/image|favicon.ico|.*\\.).*)",
  ],
};
