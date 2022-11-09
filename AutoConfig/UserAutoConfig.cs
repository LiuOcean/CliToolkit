using CliToolkit.Tools;
using Newtonsoft.Json;

namespace CliToolkit.AutoConfig;

[Serializable]
[AutoConfig(false, false)]
public class UserAutoConfig : IAutoConfig
{
    [JsonProperty]
    public string last_login { get; private set; }

    public void SetLastLogin(string user_name)
    {
        if(string.Equals(user_name, last_login))
        {
            return;
        }

        last_login = user_name;

        AutoConfigUtils.SaveAutoConfig(this);
    }

    public bool CheckPrompt() { return false; }
}