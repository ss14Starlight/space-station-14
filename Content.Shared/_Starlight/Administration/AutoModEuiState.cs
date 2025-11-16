using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Administration;

[Serializable, NetSerializable]
public sealed class AutoModEuiState : EuiStateBase
{
    public List<AutoModRule> Rules { get; set; } = new();
}

public static class AutoModEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class DeleteRuleRequest : EuiMessageBase
    {
        public AutoModRule Rule { get; set; }
        
        public DeleteRuleRequest(AutoModRule rule) => Rule = rule;
    }

    [Serializable, NetSerializable]
    public sealed class AddRuleRequest : EuiMessageBase
    {
        public AutoModRule Rule { get; set; }
        
        public AddRuleRequest(AutoModRule rule) => Rule = rule;
    }

    [Serializable, NetSerializable]
    public sealed class UpdateRuleRequest : EuiMessageBase
    {
        public AutoModRule Rule { get; set; }
        
        public UpdateRuleRequest(AutoModRule rule) => Rule = rule;
    }

    [Serializable, NetSerializable]
    public sealed class BulkUpdateRulesRequest : EuiMessageBase
    {
        public List<AutoModRule> Rules { get; set; }
        
        public BulkUpdateRulesRequest(List<AutoModRule> rules) => Rules = rules;
    }

    [Serializable, NetSerializable]
    public sealed class RefreshRequest : EuiMessageBase
    {
    }

    [Serializable, NetSerializable]
    public sealed class ValidationErrorResponse : EuiMessageBase
    {
        public string ErrorMessage { get; set; }
        public string BlacklistedWord { get; set; }
        public string Pattern { get; set; }
        
        public ValidationErrorResponse(string errorMessage, string blacklistedWord, string pattern)
        {
            ErrorMessage = errorMessage;
            BlacklistedWord = blacklistedWord;
            Pattern = pattern;
        }
    }
}
