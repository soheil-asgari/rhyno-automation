import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

type PermissionEnvelope = {
  permissions?: string[];
};

const routePermissions: Record<string, string[]> = {
  "/admin/roles-management": ["Security.Manage"],
  "/admin/audit-logs": ["AuditLogs.Read"],
};

function decodePermissions(token: string | undefined) {
  if (!token) {
    return [] as string[];
  }

  try {
    const [, payload] = token.split(".");
    const json = JSON.parse(Buffer.from(payload, "base64url").toString("utf8")) as PermissionEnvelope;
    return json.permissions ?? [];
  } catch {
    return [];
  }
}

export function middleware(request: NextRequest) {
  const matchedRoute = Object.keys(routePermissions).find((route) => request.nextUrl.pathname.startsWith(route));
  if (!matchedRoute) {
    return NextResponse.next();
  }

  const token = request.cookies.get("access_token")?.value;
  const permissions = decodePermissions(token);
  const requiredPermissions = routePermissions[matchedRoute];
  const isAllowed = requiredPermissions.some((permission) => permissions.includes(permission));

  if (!isAllowed) {
    return NextResponse.redirect(new URL("/403", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/admin/:path*"],
};
