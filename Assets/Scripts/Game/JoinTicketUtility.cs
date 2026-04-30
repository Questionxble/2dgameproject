using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class JoinTicketUtility
{
    public static bool TryValidateJoinTicket(
        string token,
        string secret,
        string expectedTargetId,
        string expectedScope,
        int clockSkewSeconds,
        out JoinTicketClaims claims,
        out string error)
    {
        claims = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            error = "Missing join ticket.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            error = "Server join-ticket secret is not configured.";
            return false;
        }

        string[] parts = token.Split('.');
        if (parts.Length != 2)
        {
            error = "Join ticket format is invalid.";
            return false;
        }

        string payloadEncoded = parts[0];
        string signatureEncoded = parts[1];

        byte[] payloadBytes;
        byte[] providedSignatureBytes;

        try
        {
            payloadBytes = Base64UrlDecode(payloadEncoded);
            providedSignatureBytes = Base64UrlDecode(signatureEncoded);
        }
        catch (FormatException)
        {
            error = "Join ticket encoding is invalid.";
            return false;
        }

        byte[] expectedSignatureBytes = ComputeSignatureBytes(payloadEncoded, secret);
        if (!FixedTimeEquals(providedSignatureBytes, expectedSignatureBytes))
        {
            error = "Join ticket signature is invalid.";
            return false;
        }

        JoinTicketPayload payload;

        try
        {
            payload = JsonUtility.FromJson<JoinTicketPayload>(Encoding.UTF8.GetString(payloadBytes));
        }
        catch (ArgumentException)
        {
            error = "Join ticket payload could not be parsed.";
            return false;
        }

        if (payload == null)
        {
            error = "Join ticket payload is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.sub))
        {
            error = "Join ticket subject is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.pn))
        {
            error = "Join ticket player name is missing.";
            return false;
        }

        long unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (payload.exp + Math.Max(0, clockSkewSeconds) < unixNow)
        {
            error = "Join ticket has expired.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedScope) && !string.Equals(payload.scp, expectedScope, StringComparison.Ordinal))
        {
            error = "Join ticket scope is invalid.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedTargetId) && !string.Equals(payload.iid, expectedTargetId, StringComparison.Ordinal))
        {
            error = "Join ticket target id is invalid.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(payload.la)
            && !string.Equals(payload.la, "create", StringComparison.Ordinal)
            && !string.Equals(payload.la, "join", StringComparison.Ordinal))
        {
            error = "Join ticket lobby action is invalid.";
            return false;
        }

        claims = new JoinTicketClaims
        {
            Subject = payload.sub,
            PlayerName = payload.pn,
            Scope = payload.scp,
            TargetId = payload.iid,
            Nonce = payload.nonce,
            ExpiresAtUnix = payload.exp,
            LobbyCode = payload.lc,
            LobbyAction = payload.la,
        };

        return true;
    }

    private static byte[] ComputeSignatureBytes(string payloadEncoded, string secret)
    {
        using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
        {
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadEncoded));
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string normalized = input.Replace('-', '+').Replace('_', '/');
        int padding = normalized.Length % 4;

        if (padding == 2)
        {
            normalized += "==";
        }
        else if (padding == 3)
        {
            normalized += "=";
        }
        else if (padding != 0)
        {
            throw new FormatException("Invalid base64url length.");
        }

        return Convert.FromBase64String(normalized);
    }

    private static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        int diff = 0;
        for (int index = 0; index < left.Length; index++)
        {
            diff |= left[index] ^ right[index];
        }

        return diff == 0;
    }

    [Serializable]
    private class JoinTicketPayload
    {
        public string sub;
        public string pn;
        public string scp;
        public string iid;
        public string lc;
        public string la;
        public string nonce;
        public long exp;
    }
}

[Serializable]
public sealed class JoinTicketClaims
{
    public string Subject;
    public string PlayerName;
    public string Scope;
    public string TargetId;
    public string LobbyCode;
    public string LobbyAction;
    public string Nonce;
    public long ExpiresAtUnix;
}