/// <reference types="astro/client" />

declare namespace App {
    interface Locals {
        user: import('./lib/server/auth').SessionUser | null;
    }
}
