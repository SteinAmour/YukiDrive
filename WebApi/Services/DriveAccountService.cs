using System.IO;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using YukiDrive.Helpers;
using YukiDrive.Contexts;
using YukiDrive.Models;

namespace YukiDrive.Services
{
    public class DriveAccountService : IDriveAccountService
    {
        private IConfidentialClientApplication app;
        private AuthenticationResult authorizeResult;
        private AuthorizationCodeProvider authProvider;
        private SiteContext siteContext;
        public DriveAccountService(SiteContext siteContext){
            app = ConfidentialClientApplicationBuilder
            .Create(Configuration.ClientId)
            .WithClientSecret(Configuration.ClientSecret)
            .WithRedirectUri(Configuration.RedirectUri)
            .Build();
            //缓存Token
            TokenCacheHelper.EnableSerialization(app.UserTokenCache);
            authProvider = new AuthorizationCodeProvider(app);
            if(File.Exists(TokenCacheHelper.CacheFilePath)){
                authorizeResult = authProvider.ClientApplication.AcquireTokenSilent(Configuration.Scopes, Configuration.AccountName).ExecuteAsync().Result;
            }
            this.siteContext = siteContext;
        }
        /// <summary>
        /// 返回 Oauth 验证url
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetAuthorizationRequestUrl(){
            var redirectUrl = await app.GetAuthorizationRequestUrl(Configuration.Scopes).ExecuteAsync();
            return redirectUrl.AbsoluteUri;
        }
        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public async Task<AuthenticationResult> Authorize(string code){
            AuthorizationCodeProvider authProvider = new AuthorizationCodeProvider(app);
            var result = await authProvider.ClientApplication.AcquireTokenByAuthorizationCode(Configuration.Scopes, code).ExecuteAsync();
            return result;
        }
        /// <summary>
        /// 添加 SharePoint Site-ID 到数据库
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="dominName"></param>
        /// <returns></returns>
        public async Task AddSiteId(string siteName)
        {
            Site site = new Site();
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                var apiCaller = new ProtectedApiCallHelper(httpClient);
                await apiCaller.CallWebApiAndProcessResultASync($"https://graph.microsoft.com/v1.0/sites/{Configuration.DominName}:/sites/{siteName}", authorizeResult.AccessToken, (result) =>
                {
                    site.SiteId = result.Properties().Single((prop) => prop.Name == "id").Value.ToString();
                    site.Name = result.Properties().Single((prop) => prop.Name == "name").Value.ToString();
                });
            }
            await siteContext.Sites.AddAsync(site);
            await siteContext.SaveChangesAsync();
        }
    }
}