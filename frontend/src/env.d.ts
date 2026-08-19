/// <reference types="astro/client" />

declare namespace App {
    interface Locals {
        user?: {
            role: string;
            email: string;
        } | null;
    }
}