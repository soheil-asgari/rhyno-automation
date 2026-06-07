"use client";

import { useMemo } from "react";
import { useRouter } from "next/navigation";
import type { AccessProfile } from "../../types/rbac";

type Props = {
  profile: AccessProfile | null;
  requiredPermissions: string[];
  fallbackPath?: string;
  children: React.ReactNode;
};

export function WithPermission({
  profile,
  requiredPermissions,
  fallbackPath = "/403",
  children,
}: Props) {
  const router = useRouter();

  const isAllowed = useMemo(() => {
    if (!profile) {
      return false;
    }

    return requiredPermissions.some((permission) => profile.permissions.includes(permission));
  }, [profile, requiredPermissions]);

  if (!isAllowed) {
    router.replace(fallbackPath);
    return null;
  }

  return <>{children}</>;
}
