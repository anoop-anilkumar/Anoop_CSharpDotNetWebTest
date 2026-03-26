# AnoopUserRegistrationTest

## Project Summary
- ASP.NET Core MVC web application built with .NET 8
- Implements registration, login, forgot password, change password, and a protected welcome page
- Uses Razor views, a service layer, cookie authentication, and encrypted JSON storage
- Built as a small authentication project with a simple MVC structure and practical security features

## Main Features
- Register a new user with first name, last name, email, password, and confirm password
- Prevent duplicate email registration
- Log in with email and password
- Remember-me option on login
- Forgot password flow
- Change password flow for authenticated users
- Welcome page protected by authorization
- Role and claim-based authentication structure
- Login lockout after repeated failed attempts

## Architecture
- `Models`
  Validation rules, view models, and user data models
- `Controllers`
  MVC actions and request handling
- `Services`
  Registration, authentication, hashing, encryption, and storage logic
- `Middleware`
  Global exception handling
- `Views`
  Razor UI pages for account and home flows

## Security Features Implemented
- Passwords are salted and hashed with SHA256 before storage
- Stored user data file is encrypted with AES-256-CBC before being written to disk
- POST actions use anti-forgery validation for CSRF protection
- Razor output encoding helps reduce XSS risk
- Cookie authentication is used for signed-in users
- Claims and roles are attached to the authenticated principal
- Data Protection keys are persisted so auth cookies survive normal restarts more reliably
- Repeated failed login attempts trigger temporary account lockout
- HTTPS redirection and HSTS are enabled
- Kestrel server header is disabled to reduce version disclosure
- Custom error page and global exception middleware are included
- Logging records registration, login, reset, and password change activity


## Data Storage
- Current storage type: encrypted JSON file
- File location: `AnoopUserRegistrationTest/App_Data/users.json`
- This keeps the project simple and easy to run without external setup
- Data is local to the machine running the app

## Authentication Notes
- The app uses ASP.NET Core cookie authentication, not JWT
- Logged-in users receive an encrypted auth cookie
- If the app stops and starts again, auth can continue as long as the cookie is still valid and Data Protection keys remain available
- Cookies are used only for authentication state, not for storing user records

## Validation Rules
- First name and last name require at least 2 characters
- Name fields allow only letters, spaces, hyphens, and apostrophes
- Email is validated with data annotations and regex
- Password requires:
  At least 8 characters
  One uppercase letter
  One lowercase letter
  One number
  One special character
- Register, forgot password, and change password all require confirm-password matching

## UI / Theme
- Blue color palette chosen to align visually with the S-Digital website style
- Focus on readability, clean layout, and simple Bootstrap-based styling
- Password visibility toggle is available on the main password fields where appropriate

## Run Without .NET 8
dotnet publish -c Release -r osx-arm64 --self-contained true
{Publish Folder} --urls http://127.0.0.1:5080