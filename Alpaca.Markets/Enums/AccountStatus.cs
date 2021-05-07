﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Alpaca.Markets
{
    /// <summary>
    /// User account status in Alpaca REST API.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum AccountStatus
    {
        /// <summary>
        /// Account opened but not verified.
        /// </summary>
        [EnumMember(Value = "ONBOARDING")]
        Onboarding,

        /// <summary>
        /// Missed important information.
        /// </summary>
        [EnumMember(Value = "SUBMISSION_FAILED")]
        SubmissionFailed,

        /// <summary>
        /// Additional information added.
        /// </summary>
        [EnumMember(Value = "SUBMITTED")]
        Submitted,

        /// <summary>
        /// Account data updated.
        /// </summary>
        [EnumMember(Value = "ACCOUNT_UPDATED")]
        AccountUpdated,

        /// <summary>
        /// Approval request sent.
        /// </summary>
        [EnumMember(Value = "APPROVAL_PENDING")]
        ApprovalPending,

        /// <summary>
        /// Account approved and working.
        /// </summary>
        [EnumMember(Value = "ACTIVE")]
        Active,

        /// <summary>
        /// Account approval rejected.
        /// </summary>
        [EnumMember(Value = "REJECTED")]
        Rejected,

        /// <summary>
        /// Account disabled.
        /// </summary>
        [EnumMember(Value = "DISABLED")]
        Disabled,

        /// <summary>
        /// Disable request sent.
        /// </summary>
        [EnumMember(Value = "DISABLE_PENDING")]
        DisablePending
    }
}
