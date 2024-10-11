using System.Text;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using TelegramBot.Utilities.EnvironmentUtilities;

namespace TelegramBot.Commands.Major.ProjectCommand
{
    public class JenkinsProject
    {
        public static async Task<List<string>> GetJenkinsProjectsAsync(long userId, string userRole)
        {
            try
            {
                string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(jenkinsUrl);
                    var credentials = EnvironmentVariableLoader.GetCredentialsForRole(userRole);

                    Console.WriteLine($"Received role: {userRole} for user ID: {userId}");

                    var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                    var response = await client.GetAsync("/api/json?tree=jobs[name]");
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);

                    return json["jobs"].Select(job => job["name"].ToString()).ToList();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Unauthorized access: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving Jenkins projects: {ex.Message}");
                throw new Exception("Failed to retrieve Jenkins projects", ex);
            }
        }
    }
}
