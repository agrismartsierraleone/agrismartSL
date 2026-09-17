using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthAppService authAppService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await authAppService.RegisterAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authAppService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Registers (or replaces) the calling device's biometric public key. Call right after a normal login, with that login's JWT.</summary>
    [Authorize]
    [HttpPost("biometric/register")]
    public async Task<IActionResult> RegisterBiometric(BiometricRegisterRequest request, CancellationToken cancellationToken)
    {
        await authAppService.RegisterBiometricAsync(currentUser.UserId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Turns off biometric login for the calling device.</summary>
    [Authorize]
    [HttpDelete("biometric/{deviceId}")]
    public async Task<IActionResult> UnregisterBiometric(string deviceId, CancellationToken cancellationToken)
    {
        await authAppService.UnregisterBiometricAsync(currentUser.UserId, deviceId, cancellationToken);
        return NoContent();
    }

    /// <summary>Step 1 of biometric login: get a one-time challenge to sign. Public — the device isn't holding a valid JWT at this point.</summary>
    [HttpPost("biometric/challenge")]
    public async Task<ActionResult<BiometricChallengeResponse>> RequestBiometricChallenge(BiometricChallengeRequest request, CancellationToken cancellationToken)
    {
        var response = await authAppService.RequestBiometricChallengeAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Step 2 of biometric login: submit the signed challenge and receive a normal JWT back.</summary>
    [HttpPost("biometric/login")]
    public async Task<ActionResult<AuthResponse>> LoginWithBiometric(BiometricLoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authAppService.LoginWithBiometricAsync(request, cancellationToken);
        return Ok(response);
    }
}
