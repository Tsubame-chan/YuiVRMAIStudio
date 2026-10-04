using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.Api
{
    public sealed partial class YuiBackendClient
    {
        private static string CompanionCharacterPath(string characterId, string action)
        {
            if (characterId == null || characterId.Length != 32) throw new ArgumentException("Invalid character ID.");
            foreach (var c in characterId)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Invalid character ID.");
            return "/companion/v2/characters/" + characterId + "/" + action;
        }

        public Task<JObject> CompanionCapabilitiesAsync(string pairedToken, CancellationToken cancellation = default)
            => CompanionAsync("/companion/v2/capabilities", null, pairedToken, cancellation);

        public Task<JObject> CompanionIssueGrantAsync(string connectionId, string characterId,
            string pairedToken, CancellationToken cancellation = default)
            => CompanionAsync("/companion/v2/grants", new JObject {
                ["connection_id"] = connectionId,
                ["character_ids"] = new JArray(characterId),
                ["operations"] = new JArray("read_inbox", "acknowledge", "post_update"),
                ["ttl_seconds"] = 30 * 86400
            }, pairedToken, cancellation);

        public Task<JObject> CompanionIssueOAuthApprovalAsync(string grantId,
            string pairedToken, CancellationToken cancellation = default)
        {
            if (grantId == null || grantId.Length != 32) throw new ArgumentException("Invalid grant ID.");
            foreach (var c in grantId)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Invalid grant ID.");
            return CompanionAsync("/companion/v2/grants/" + grantId + "/oauth-approval",
                                  new JObject(), pairedToken, cancellation);
        }

        public Task<JObject> CompanionRevokeGrantAsync(string grantId, string pairedToken,
            CancellationToken cancellation = default)
        {
            if (grantId == null || grantId.Length != 32) throw new ArgumentException("Invalid grant ID.");
            foreach (var c in grantId)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Invalid grant ID.");
            if (string.IsNullOrWhiteSpace(pairedToken)) throw new ArgumentException("Paired device token required.");
            return PairedJsonAsync("/companion/v2/grants/" + grantId, null, pairedToken,
                                   cancellation, "DELETE");
        }

        public Task<JObject> CompanionCommitAsync(string characterId, YuiCompanionCommitRequest batch,
            string pairedToken, CancellationToken cancellation = default)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            return CompanionAsync(CompanionCharacterPath(characterId, "commit"), JObject.FromObject(batch),
                                  pairedToken, cancellation);
        }

        public async Task<YuiCompanionChangePage> CompanionChangesAsync(string characterId, string cursor,
            string pairedToken, CancellationToken cancellation = default)
        {
            var path = CompanionCharacterPath(characterId, "changes");
            if (!string.IsNullOrEmpty(cursor)) path += "?cursor=" + Uri.EscapeDataString(cursor);
            var result = await CompanionAsync(path, null, pairedToken, cancellation);
            return result.ToObject<YuiCompanionChangePage>();
        }

        public async Task<YuiCompanionSnapshotPage> CompanionSnapshotAsync(string characterId, string cursor,
            string pairedToken, CancellationToken cancellation = default)
        {
            var path = CompanionCharacterPath(characterId, "snapshot");
            if (!string.IsNullOrEmpty(cursor)) path += "?cursor=" + Uri.EscapeDataString(cursor);
            var result = await CompanionAsync(path, null, pairedToken, cancellation);
            return result.ToObject<YuiCompanionSnapshotPage>();
        }

        public async Task<YuiCompanionContextPacket> CompanionContextAsync(string characterId,
            YuiCompanionContextRequest request, string pairedToken, CancellationToken cancellation = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var result = await CompanionAsync(CompanionCharacterPath(characterId, "context"),
                                              JObject.FromObject(request), pairedToken, cancellation);
            return result.ToObject<YuiCompanionContextPacket>();
        }

        private static string CompanionActivityPath(string characterId, string activityId)
        {
            if (activityId == null || activityId.Length != 32) throw new ArgumentException("Invalid activity ID.");
            foreach (var c in activityId)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Invalid activity ID.");
            return CompanionCharacterPath(characterId, "activities/" + activityId);
        }

        public async Task<YuiCompanionActivityReceipt> CompanionCreateActivityAsync(string characterId,
            YuiCompanionActivityCreate request, string pairedToken, CancellationToken cancellation = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var result = await CompanionAsync(CompanionCharacterPath(characterId, "activities"),
                                              JObject.FromObject(request), pairedToken, cancellation);
            return result.ToObject<YuiCompanionActivityReceipt>();
        }

        public Task<JObject> CompanionActivityAsync(string characterId, string activityId,
            string pairedToken, CancellationToken cancellation = default)
            => CompanionAsync(CompanionActivityPath(characterId, activityId), null, pairedToken, cancellation);

        public async Task<YuiCompanionActivityList> CompanionActivitiesAsync(string characterId,
            string connectionId, string pairedToken, CancellationToken cancellation = default,
            string before = null)
        {
            var path = CompanionCharacterPath(characterId, "activities") + "?limit=100";
            if (!string.IsNullOrEmpty(connectionId))
                path += "&connection_id=" + Uri.EscapeDataString(connectionId);
            if (!string.IsNullOrEmpty(before))
                path += "&before=" + Uri.EscapeDataString(before);
            var result = await CompanionAsync(path, null, pairedToken, cancellation);
            return result.ToObject<YuiCompanionActivityList>();
        }

        public async Task<YuiCompanionActivityDetails> CompanionActivityDetailsAsync(
            string characterId, string activityId, string pairedToken,
            CancellationToken cancellation = default)
        {
            var result = await CompanionActivityAsync(characterId, activityId, pairedToken, cancellation);
            return result.ToObject<YuiCompanionActivityDetails>();
        }

        private static string CompanionReportPath(string characterId, string activityId, string reportOpId)
        {
            if (reportOpId == null || reportOpId.Length != 32)
                throw new ArgumentException("Invalid report ID.");
            foreach (var c in reportOpId)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Invalid report ID.");
            return CompanionActivityPath(characterId, activityId) + "/reports/" + reportOpId + "/speech";
        }

        public async Task<YuiCompanionSpeechClaim> CompanionClaimSpeechAsync(string characterId,
            string activityId, string reportOpId, string pairedToken,
            CancellationToken cancellation = default)
        {
            var result = await CompanionAsync(CompanionReportPath(characterId, activityId, reportOpId)
                + "/claim", new JObject(), pairedToken, cancellation);
            return result.ToObject<YuiCompanionSpeechClaim>();
        }

        public Task<JObject> CompanionSpeechReceiptAsync(string characterId, string activityId,
            string reportOpId, string stage, string claimToken, string pairedToken,
            CancellationToken cancellation = default)
        {
            if (stage != "started" && stage != "completed") throw new ArgumentException("Invalid stage.");
            return CompanionAsync(CompanionReportPath(characterId, activityId, reportOpId)
                + "/" + stage, new JObject { ["claim_token"] = claimToken }, pairedToken, cancellation);
        }

        public async Task<YuiCompanionActivityReceipt> CompanionActivityCommandAsync(string characterId,
            string activityId, YuiCompanionActivityCommand command, string pairedToken,
            CancellationToken cancellation = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var result = await CompanionAsync(CompanionActivityPath(characterId, activityId) + "/commands",
                                              JObject.FromObject(command), pairedToken, cancellation);
            return result.ToObject<YuiCompanionActivityReceipt>();
        }
    }
}
