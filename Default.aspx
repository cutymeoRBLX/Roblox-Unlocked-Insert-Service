<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="UnlockedInsertService.DefaultPage" CompilationMode="Auto" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Unlocked InsertService Server for Roblox</title>
    <link rel="stylesheet" type="text/css" href="style.css" />
</head>
<body>
    <div class="UiContentBox">
        <% if (_LoggedInUserId.HasValue) { %>
        <h1>You're good to go!</h1>
        <p>This server can now help you bypass InsertService restrictions!</p>
        <p>Please continue reading the guide so you can complete the setup process.</p>
        <p><strong>Your user ID is <%=_LoggedInUserId.Value.ToString() %></strong></p>
        <% } else { %>
        <h1>The server cannot log into Roblox.</h1>
        <p>The server is unable to access Roblox on your behalf. Please use your server's admin console and edit the <strong>Web.config</strong> file with your current ROBLOSECURITY cookie.</p>
        <% } %>
    </div>
</body>
</html>
