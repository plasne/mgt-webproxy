using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;

public interface ITokenValidator
{
    Task<JwtSecurityToken> ValidateToken(string token);
}