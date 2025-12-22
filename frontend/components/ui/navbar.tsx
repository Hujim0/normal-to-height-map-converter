"use client";

import { cn } from "@/lib/utils";
import { ModeToggle } from "./mode-toggle";
import { NavigationMenu, NavigationMenuItem, NavigationMenuLink, NavigationMenuList } from "./navigation-menu";
import Link from "next/link";
import { Label } from "./label";

export function Navbar({ className }: { className?: string }) {
  return (
    <div className={cn("flex justify-between", className)}>
      <NavigationMenu viewport={false}>
        <NavigationMenuList className="flex-wrap">
          <NavigationMenuItem>
            <NavigationMenuLink asChild>
              <Link className="text-lg bold" href="/">
                Normal map integration
              </Link>
            </NavigationMenuLink>
          </NavigationMenuItem>
          <NavigationMenuItem>
            <NavigationMenuLink asChild>
              <Link className="text-lg bold" href="/examples">
                examples
              </Link>
            </NavigationMenuLink>
          </NavigationMenuItem>
        </NavigationMenuList>
      </NavigationMenu>
      <ModeToggle></ModeToggle>
    </div>

  )
}
