using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stl.Security 
{
    public static class KeyVaultEx
    {
        public static string GetSecret(this IKeyVault keyVault, string key)
            => keyVault.TryGetSecret(key) ?? throw new KeyNotFoundException();
        public static async ValueTask<string> GetSecretAsync(this IKeyVault keyVault, string key)
            => (await keyVault.TryGetSecretAsync(key)) ?? throw new KeyNotFoundException();

        public static IKeyVault WithPrefix(this IKeyVault keyVault, string prefix)
            => new PrefixScopedKeyVault(keyVault, prefix);
    }
}