namespace OperatorPhone
{
    /// <summary>
    /// Service endpoints baked into the build so the mod works on install with no
    /// configuration step.
    ///
    /// The Photon App ID is not a secret and cannot be made one — it ships inside every
    /// Photon client ever built and is trivially extractable from this DLL. That's fine:
    /// the App ID alone gets an attacker nothing, because the Chat app requires custom
    /// authentication against our Worker, and the Worker only vouches for tokens issued
    /// through a verified Steam OpenID login.
    ///
    /// Never put the Worker's IDENTITY_SECRET here, or anything else that grants
    /// authority rather than merely naming a service.
    /// </summary>
    internal static class ServiceConfig
    {
        /// <summary>Photon Chat App ID. Dashboard > your Chat app.</summary>
        public const string PhotonChatAppId = "REPLACE_WITH_YOUR_CHAT_APP_ID";

        /// <summary>Identity worker base URL, no trailing slash.</summary>
        public const string WorkerUrl = "https://operatorphone.operatorphone.workers.dev";

        /// <summary>
        /// Bumping this partitions the Photon network: clients on different versions
        /// cannot see or message each other. Raise it only for genuinely incompatible
        /// protocol changes, not routine releases.
        /// </summary>
        public const string PhotonAppVersion = "1";
    }
}