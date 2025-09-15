# Registration Flow Test Guide

## Fixed Issues

### 1. Step 1 (Email Input)
- ✅ Fixed form submission to use simple string parameter instead of complex ViewModel
- ✅ Added proper email validation
- ✅ Fixed step transition to Step 2

### 2. Step 2 (2FA Verification)
- ✅ Fixed form binding to use simple string parameters
- ✅ Fixed TempData persistence between steps
- ✅ Fixed step transition to Step 3
- ✅ Added proper error handling for invalid/expired codes

### 3. Step 3 (Complete Registration)
- ✅ Fixed form binding to use simple string parameters
- ✅ Added proper validation for all fields
- ✅ Fixed Identity user creation
- ✅ Fixed custom User record creation
- ✅ Added proper error handling and user feedback

### 4. JavaScript Improvements
- ✅ Fixed step management and form validation
- ✅ Added proper data persistence between steps
- ✅ Fixed hidden field population
- ✅ Added client-side validation

## How to Test

1. **Start the application:**
   ```bash
   cd /Users/chloetang/Documents/Certio
   dotnet run --project Certio.Web
   ```

2. **Navigate to registration:**
   - Go to `http://localhost:5000/Home/Register`
   - You should see Step 1 (Email Input)

3. **Test Step 1:**
   - Enter a valid email address
   - Click "Continue"
   - You should be redirected to Step 2
   - Check the console for the verification code (it will be printed there for development)

4. **Test Step 2:**
   - Enter the 6-digit verification code from the console
   - Click "Verify Code"
   - You should be redirected to Step 3

5. **Test Step 3:**
   - Fill in First Name, Last Name, Password, and Confirm Password
   - Click "Create Account"
   - You should be redirected to the Projects page
   - A new user should be created in the database

## Expected Behavior

- **Step 1:** Email validation, user existence check, 2FA code generation
- **Step 2:** Code verification, proper error handling
- **Step 3:** User creation (both Identity and custom User), automatic sign-in
- **Success:** Redirect to Projects page with success message

## Debug Information

- Verification codes are printed to the console for development
- All form submissions use simple string parameters for better reliability
- TempData is properly managed between steps
- Error messages are displayed to the user
- Success messages confirm account creation

## Database Changes

- Creates Identity user with email as username
- Creates custom User record with additional profile information
- Sets proper user type (Client) and default values
- Establishes proper relationships between Identity and custom User
