# MSAL Setup Guide for Graph Explorer

This guide walks you through setting up Azure Active Directory (Microsoft Entra ID) authentication for Graph Explorer to connect to Azure Digital Twins instances using MSAL (Microsoft Authentication Library) with PKCE flow.

## Prerequisites

- An Azure subscription
- An Azure Digital Twins instance
- Permission to register applications in your Azure AD tenant

## Step 1: Register Application in Microsoft Entra ID

### 1.1 Navigate to App Registrations

1. Sign in to the [Azure Portal](https://portal.azure.com)
2. Navigate to **Microsoft Entra ID** (formerly Azure Active Directory)
3. Select **App registrations** from the left menu
4. Click **+ New registration**

### 1.2 Configure Application Registration

**Application Name:**

- Enter a descriptive name (e.g., "Graph Explorer - MSAL")

**Supported Account Types:**

- Select **Accounts in this organizational directory only** (single tenant)
- Or **Accounts in any organizational directory** (multi-tenant) if you need to support multiple tenants

**Redirect URI:**

- Platform: **Single-page application (SPA)**
- URI: `http://localhost:5173` (for local development)
- URI: `https://your-production-domain.com` (for production)

> **Note:** You can add multiple redirect URIs after registration if you need to support both local and production environments.

Click **Register**.

### 1.3 Note Your Application IDs

After registration, note the following from the **Overview** page:

- **Application (client) ID** - You'll need this for the Graph Explorer connection
- **Directory (tenant) ID** - You'll need this for the Graph Explorer connection

## Step 2: Configure API Permissions

### 2.1 Add Azure Digital Twins Permission

1. In your app registration, select **API permissions** from the left menu
2. Click **+ Add a permission**
3. Select **APIs my organization uses**
4. Search for **Azure Digital Twins**
5. Select **Azure Digital Twins**
6. Click **Delegated permissions**
7. Check **user_impersonation**
8. Click **Add permissions**

### 2.2 Grant Admin Consent (if required)

If your organization requires admin consent for API permissions:

1. Click **Grant admin consent for [Your Tenant]**
2. Confirm by clicking **Yes**

You should see a green checkmark next to the permission indicating it's been granted.

## Step 3: Configure Authentication Settings

### 3.1 Enable Public Client Flow (Optional)

1. Select **Authentication** from the left menu
2. Scroll down to **Advanced settings**
3. Under **Allow public client flows**, select **Yes** (only if needed for mobile/desktop scenarios)
4. Click **Save**

### 3.2 Configure Token Configuration (Optional)

For additional claims in tokens:

1. Select **Token configuration** from the left menu
2. Add optional claims as needed (typically not required for basic scenarios)

## Step 4: Configure Connection in Graph Explorer

### 4.1 Add New Connection

1. Open Graph Explorer
2. Click **Add** next to the connection dropdown
3. Fill in the connection details:
   - **Name:** Azure Digital Twins Production
   - **Host:** `<your-adt-instance>.api.<region>.digitaltwins.azure.net`
   - **Description:** (optional)
   - **Authentication:** Select **MSAL (Azure Digital Twins)**

### 4.2 Enter MSAL Configuration

- **Client ID:** Paste the **Application (client) ID** from Step 1.3
- **Tenant ID:** Paste the **Directory (tenant) ID** from Step 1.3
- **Scopes:** (optional) Leave empty to use default `https://digitaltwins.azure.net/.default`

Click **Add Connection**.

## Step 5: Test Authentication

1. Select your new connection from the dropdown
2. Graph Explorer will redirect you to the Microsoft sign-in page
3. Sign in with your Azure AD account
4. Grant consent when prompted
5. You'll be redirected back to Graph Explorer
6. You should now be authenticated and able to query your Digital Twins instance

## Troubleshooting

### "AADSTS50011: Reply URL mismatch"

**Problem:** The redirect URI doesn't match what's registered in Azure AD.

**Solution:**

1. Check the redirect URI in your app registration (Authentication > Redirect URIs)
2. Ensure it matches exactly: `http://localhost:5173` for local or your production URL
3. Add the correct URI if missing
4. Wait a few minutes for changes to propagate

### "AADSTS65001: Consent not granted"

**Problem:** User hasn't consented to the API permissions.

**Solution:**

1. Ensure admin consent is granted (Step 2.2)
2. Or ensure the user has permission to consent to applications
3. Try signing in again and carefully review the consent prompt

### "Insufficient permissions"

**Problem:** The app doesn't have the required API permissions.

**Solution:**

1. Verify Azure Digital Twins API permission is added (Step 2.1)
2. Verify `user_impersonation` permission is selected
3. Ensure admin consent is granted if required
4. Verify the user has appropriate RBAC roles on the Digital Twins instance:
   - **Azure Digital Twins Data Reader** (read-only)
   - **Azure Digital Twins Data Owner** (read/write)

### "Token acquisition failed"

**Problem:** MSAL can't acquire a token.

**Solution:**

1. Clear browser local storage and cookies
2. Sign out and sign in again
3. Check browser console for detailed error messages
4. Verify Client ID and Tenant ID are correct

## Security Best Practices

### Production Deployments

1. **Use HTTPS:** Always use HTTPS redirect URIs in production
2. **Restrict Redirect URIs:** Only add the specific URIs you need
3. **Limit Permissions:** Only request the minimum required API permissions
4. **Monitor Sign-ins:** Regularly review sign-in logs in Azure AD

### Token Handling

- Tokens are stored in browser local storage by MSAL
- Tokens are automatically refreshed by MSAL before expiration
- Clear tokens by signing out or clearing browser data

## Additional Resources

- [Microsoft Entra ID Documentation](https://learn.microsoft.com/entra/identity/)
- [MSAL.js Documentation](https://learn.microsoft.com/entra/msal/javascript/)
- [Azure Digital Twins Authentication](https://learn.microsoft.com/azure/digital-twins/how-to-authenticate-client)
- [PKCE Flow Overview](https://learn.microsoft.com/entra/identity-platform/v2-oauth2-auth-code-flow)

## Support

For issues specific to Graph Explorer authentication, please check:

- [GitHub Issues](https://github.com/konnektr-io/pg-age-digitaltwins/issues)
- [Documentation](https://docs.konnektr.io/docs/graph/)

For Azure Digital Twins or Azure AD issues:

- [Azure Support](https://azure.microsoft.com/support/)
- [Microsoft Q&A](https://learn.microsoft.com/answers/)
