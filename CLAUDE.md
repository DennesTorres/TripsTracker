# TripsTracker

## Purpose
Personal trip history tracking application for individuals.

## Tech Stack
- **Frontend**: React + TypeScript + D3.js + shadcn/ui + Zustand + Axios + React Query
- **Backend**: Azure Functions isolated worker (.NET 10) + EF Core 10 + Azure SQL
- **Auth**: Function key only (MVP); Entra External ID planned for Stage 3+

## Repository
https://github.com/DennesTorres/TripsTracker

## Solution
`TripsTracker.slnx` — VS 2022 17.10+ XML format

## Current Stage
Stage 2 UI in progress. Active branch: `feature/story-129-city-suggestions`.
See `memory/WORKFLOW.md` for current state and next sequence.

## Local Setup (gitignored — each developer configures)
- `src/TripsTracker.Web/.env.production.local` → `VITE_API_BASE_URL=http://localhost:7077`
- `src/TripsTracker.Functions/local.settings.json` → CORS origin `http://localhost:4173`, port 7077
- `TripsTracker.slnLaunch.user` → "Front and Back" multi-startup profile
- `TripsTracker.Web.esproj.user` → Chrome launch target
