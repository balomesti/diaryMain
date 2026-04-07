# Inkly

Inkly is a simple digital diary app where you can:
- Create diary entries (title + notes + date)
- Optionally attach an image to an entry
- View your recent entries on the Home page
- View all entries in “My Diary”
- Delete an entry you no longer want

This project runs locally on your computer and is split into two parts:
- **Diary App (Frontend)**: what you see in the browser (`diary-app`)
- **Diary API (Backend)**: saves/reads your data (`diary-api`)

## Quick Start (Most People)

### What you need
- .NET SDK installed (you can install it from Microsoft’s website)


### Start the backend (Diary API)
Open a terminal in the project folder and run:

```powershell
cd diary-api
dotnet run
```

When it’s running, it will say it is listening on:
- `http://localhost:5105`

You can also open Swagger (API testing page):
- `http://localhost:5105/docs`


### Start the frontend (Diary App)
Open another terminal and run:

```powershell
cd diary-app
dotnet run
```

Open the app in your browser:
- `http://localhost:5173`

## How to Use Inkly (No Developer Knowledge Needed)

1. Open the app: `http://localhost:5173`
2. Register an account (Sign Up)
3. Login
4. Create a new entry (New Entry)
5. View your entries (My Diary)
6. Delete an entry using the Delete button

## Where Your Data Is Stored

- Your data is saved in a local SQLite file on your computer:
  - `diary-api/diary.db`
- Uploaded images are saved here:
  - `diary-api/wwwroot/uploads`

## API Endpoints (Simple List)

Base URL (when running locally): `http://localhost:5105`

### Auth
- `POST /api/Auth/register` (create account)
- `POST /api/Auth/login` (login, returns token)

### Diary (requires login)
- `GET /api/Diary` (list your entries)
- `POST /api/Diary` (create entry, supports image upload)
- `DELETE /api/Diary/{id}` (delete entry)

## If Something Doesn’t Work

- If the browser shows “API not running”:
  - Start `diary-api` first (`dotnet run` inside `diary-api`)
- If Swagger shows 401 Unauthorized for diary endpoints:
  - Login first to get a token, then click **Authorize** in Swagger and paste the token
- If the app can’t load images:
  - Make sure the API is running (images are served from the API)

## For Developers (Optional)

- Frontend: Blazor WebAssembly (.NET 8)
- Backend: ASP.NET Core Web API (.NET 10)
- Database: SQLite + Entity Framework Core

Useful starting points:
- API configuration: `diary-api/Program.cs`
- API endpoints: `diary-api/Controllers/`
- Frontend API calls: `diary-app/Services/`

## API Documentation
- Swagger UI: `http://localhost:5105/docs`
- Raw JSON: `http://localhost:5105/docs/v1/swagger.json`
# diaryMain
