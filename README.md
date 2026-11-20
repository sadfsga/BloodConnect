# Blood Connect

A REST API that connects people who urgently need blood with nearby, compatible, eligible donors.

## The problem
When a patient needs blood, families post on Facebook/WhatsApp groups. Posts get buried, reach random people (wrong blood type, too far away, donated last month), and nobody knows when a request is already covered.

## The solution
Blood Connect matches by **blood-type compatibility + distance + donor eligibility**:
- Requests carry blood type, units, hospital, location and urgency (Normal / Urgent / Critical).
- Donors see only requests they can actually help with, closest and most urgent first.
- A donor pledges; the requester confirms the donation; the donor is then locked out for 56 days.
- Patient name and phone numbers are private (visible only to the owner, or to a donor who pledged).

## Tech
ASP.NET Core Web API (.NET 10) · EF Core + SQL Server · JWT auth · BCrypt · Haversine distance + bounding-box pre-filter · OpenAPI + Scalar docs.

## Web dashboard
The API also serves a single-page dashboard from `wwwroot/index.html` (open `http://localhost:5117`):
live stats, sign-in/register, a map of nearby requests (Leaflet + OpenStreetMap), request creation by clicking the map,
pledging, and owner-side confirmation of donations.

In Development the app applies migrations and seeds demo data on first start
(24 donors, 12 requests around Cairo). Demo accounts: `demo1@demo.bloodconnect` ... `demo24@demo.bloodconnect`, password `Demo@12345`.

## Run it
```bash
dotnet --version                     # must print 10.x

# 1. create the project and copy this repo's folders over it
dotnet new webapi --use-controllers -n BloodConnect.Api
cd BloodConnect.Api
# copy Controllers/ Data/ Dtos/ Models/ Options/ Services/ Program.cs appsettings.json over the generated ones
# delete the generated WeatherForecast files

# 2. packages
# note: on .NET 10 pin the 10.0.0 versions (the newest versions target .NET 11)
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.0
dotnet add package BCrypt.Net-Next
dotnet add package Scalar.AspNetCore

# 3. secret JWT key (never commit it)
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "put-a-long-random-string-of-32-plus-characters-here"

# 4. database
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial
dotnet ef database update

# 5. go
dotnet run
```
Open `/scalar` (URL printed in the console) to try every endpoint, or use `BloodConnect.http`.

## Endpoints
| Method | Route | Notes |
|---|---|---|
| POST | /api/auth/register, /api/auth/login | returns JWT |
| GET/PUT | /api/profile/me | profile, blood type, location, availability |
| GET | /api/profile/pledges | my pledges |
| GET | /api/requests | filters: bloodType, city, status, lat/lng/radiusKm, compatibleWithMe, page |
| POST | /api/requests | create (max 5 open per user) |
| GET | /api/requests/{id}, /api/requests/mine | details / my requests |
| POST/DELETE | /api/requests/{id}/pledge | pledge / cancel |
| POST | /api/requests/{id}/pledges/{pledgeId}/confirm | owner confirms donation |
| POST | /api/requests/{id}/cancel | owner closes request |
| GET | /api/stats | public totals |

## Roadmap
SignalR real-time alerts · background job to expire old requests · Flutter app.
