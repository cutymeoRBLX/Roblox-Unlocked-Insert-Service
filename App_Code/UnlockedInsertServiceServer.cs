using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Web;
using System.Configuration;

namespace UnlockedInsertService
{
    internal static class Secrets
    {
        public static string RbxAuthToken { get { return ConfigurationManager.AppSettings["RbxAuthToken"]; } }
        public static string ApiKey { get { return ConfigurationManager.AppSettings["ApiKey"]; } }
    }
    internal static class AssetBot
    {
        public static ulong WhitelistModel(ulong assetId)
        {
            if (!WhitelistCache.ContainsKey(assetId) || DateTime.UtcNow > WhitelistCache[assetId].ExpirationDate)
            {
                // Extract info from the page.
                AssetPageInfo info = GetAssetPage(assetId);

                // Ensure the thing is a model.
                if (info.AssetType != "Model")
                    throw new FormatException("The asset requested is not a Model.");

                // Post the take request.
                if (!info.UserOwnsAsset)
                {
                    if (!info.IsOnSale)
                        throw new Exception("The asset cannot be obtained!");

                    string requestString = string.Format(TakeItemUrl, info.ProductId /*, info.SellerId*/);
                    string payload = string.Format(
                        "{{\"expectedCurrency\":1,\"expectedPrice\":{1},\"expectedSellerId\":{0}}}",
                        info.SellerId, 0);
                    using (WebClient client = NewPseudoHumanClient())
                    {
                        client.Headers["X-CSRF-TOKEN"] = info.XcsrfToken;
                        client.Headers["content-type"] = "application/json; charset=utf-8";

                        client.UploadString(requestString, payload);
                        Debug.WriteLine("Whitelisted " + assetId + ".");
                        WhitelistCache[assetId] = new AssetIdCache(assetId, assetId, DateTime.UtcNow + new TimeSpan(1, 0, 0, 0));
                        return assetId;
                    }
                }
                else
                {
                    WhitelistCache[assetId] = new AssetIdCache(assetId, assetId, DateTime.UtcNow + new TimeSpan(1, 0, 0, 0));
                    return assetId;
                }
            }
            else
            {
                Debug.WriteLine("The asset ID " + assetId + " has recently been whitelisted. Using cache.");
                return WhitelistCache[assetId].UsableAssetId;
            }
        }
        public static ulong? CheckRbxUserId()
        {
            using (WebClient client = NewPseudoHumanClient())
            {
                string response = client.DownloadString("https://assetgame.roblox.com/game/GetCurrentUser.ashx");
                ulong userId;
                return ulong.TryParse(response, out userId) ? userId : (ulong?)null;
            }
        }

        private const string TakeItemUrl =
            //"https://www.roblox.com/api/item.ashx?rqtype=purchase&productID={0}&expectedCurrency=1&expectedPrice=0&expectedSellerID={1}&userAssetID=";
            "https://economy.roblox.com/v1/purchases/products/{0}";

        // RAM-Based cache that helps prevent spamming the Roblox website with the same asset IDs.
        private static readonly Dictionary<ulong, AssetIdCache> WhitelistCache = new Dictionary<ulong, AssetIdCache>();

        private static WebClient NewPseudoHumanClient()
        {
            WebClient client = new WebClient();
            client.Headers["Cookie"] = ".ROBLOSECURITY=" + Secrets.RbxAuthToken + ";";
            return client;
        }
        private static AssetPageInfo GetAssetPage(ulong assetId)
        {
            // Look at the page.
            WebClient client = NewPseudoHumanClient();
            try
            {
                string page = client.DownloadString("https://www.roblox.com/library/" + assetId.ToString() + "/");

                // Extract info from the page.
                return new AssetPageInfo(page);
            }
            finally
            { client.Dispose(); }
        }
    }
    internal struct AssetIdCache
    {
        public ulong SourceAssetId;
        public ulong UsableAssetId;
        public DateTime ExpirationDate;

        public AssetIdCache(ulong sourceAssetId, ulong usableAssetId, DateTime expirationDate)
        {
            SourceAssetId = sourceAssetId;
            UsableAssetId = usableAssetId;
            ExpirationDate = expirationDate;
        }
    }
    internal struct AssetPageInfo
    {
        public string Page;
        public ulong SellerId;
        public ulong ProductId;
        public string AssetType;
        public bool IsOnSale;
        public bool UserOwnsAsset;
        public string XcsrfToken;

        public AssetPageInfo(string page)
        {
            Page = page;
            SellerId = ulong.Parse(ExtractStringByBrackets(page, "data-expected-seller-id=\"", "\"", 64));
            ProductId = ulong.Parse(ExtractStringByBrackets(page, "data-product-id=\"", "\"", 64));
            AssetType = ExtractStringByBrackets(page, "data-asset-type=\"", "\"", 64);
            IsOnSale = ExtractStringByBrackets(page, "data-is-purchase-enabled=\"", "\"", 64) == "true";
            UserOwnsAsset = page.IndexOf("This item is available in your inventory.") != -1;
            XcsrfToken = ExtractStringByBrackets(page, "Roblox.XsrfToken.setToken('", "');", 32);
        }

        private static string ExtractStringByBrackets(string document, string leftBracket, string rightBracket, int maxLength)
        {
            int indice1 = document.IndexOf(leftBracket);
            int indice2 = indice1 + leftBracket.Length;
            int indice3 = document.IndexOf(rightBracket, indice2);
            if (indice1 == -1 || indice3 == -1 || indice3 - indice2 > maxLength)
                throw new IndexOutOfRangeException("Cannot find the bracketed string.");
            return document.Substring(indice2, indice3 - indice2);
        }
    }
    public partial class DefaultPage : System.Web.UI.Page
    {
        protected ulong? _LoggedInUserId = null;

        protected void Page_Load(object sender, EventArgs e)
        {
            _LoggedInUserId = AssetBot.CheckRbxUserId();
        }
    }
    public class WhitelistHandler : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            if (context.Request.RequestType != "POST")
            {
                context.Response.StatusCode = 400;
                return;
            }

            System.Collections.Specialized.NameValueCollection form = context.Request.Form;

            string secret = form["Secret"];
            string requestedAssetId = form["AssetID"];
            ulong assetId = 0;

            if (secret == null || requestedAssetId == null || !ulong.TryParse(requestedAssetId, out assetId))
            {
                context.Response.StatusCode = 400;
                return;
            }
            if (secret != Secrets.ApiKey)
            {
                context.Response.StatusCode = 401;
                return;
            }

            ulong usableAssetId = AssetBot.WhitelistModel(assetId);

            context.Response.ContentType = "text/plain";
            context.Response.Write(usableAssetId.ToString());
            context.Response.Flush();
        }

        public bool IsReusable { get { return false; } }
    }
}