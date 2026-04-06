using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (a, b, c, d) => true };
        var client = new HttpClient(handler);
        
        // 1. GET to retrieve token and cookie
        var response = await client.GetAsync("https://localhost:7129/");
        var html = await response.Content.ReadAsStringAsync();
        
        var match = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        if (!match.Success) {
            Console.WriteLine("Could not find AntiForgeryToken");
            return;
        }
        var token = match.Groups[1].Value;
        
        // Extract cookies
        var cookies = string.Join("; ", response.Headers.GetValues("Set-Cookie"));
        
        // 2. POST with data
        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:7129/Cart/AddToCart");
        request.Headers.Add("Cookie", cookies);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("imageUrl", "/test.jpg"),
            new KeyValuePair<string, string>("name", "Test"),
            new KeyValuePair<string, string>("price", "100000"),
            new KeyValuePair<string, string>("productId", "1"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        });
        
        var postResponse = await client.SendAsync(request);
        var postContent = await postResponse.Content.ReadAsStringAsync();
        
        if (!postResponse.IsSuccessStatusCode) {
            Console.WriteLine($"Failed with status {postResponse.StatusCode}");
            // Extract the exception logic if it's a dev exception page
            var exMatch = Regex.Match(postContent, @"class=""titleerror"">([^<]+)<");
            var traceMatch = Regex.Match(postContent, @"class=""rawExceptionStackTrace"">([^<]+)<");
            
            if (exMatch.Success) Console.WriteLine("Exception: " + exMatch.Groups[1].Value.Trim());
            if (traceMatch.Success) Console.WriteLine("Stack Trace: " + traceMatch.Groups[1].Value.Trim());
            if (!exMatch.Success && !traceMatch.Success) Console.WriteLine("Raw body: " + postContent.Substring(0, Math.Min(postContent.Length, 1500)));
        } else {
            Console.WriteLine("Success: " + postContent);
        }
    }
}
