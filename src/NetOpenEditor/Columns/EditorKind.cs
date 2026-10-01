namespace NetOpenEditor.Columns;

public enum EditorKind
{
    Text,
    Integer,
    Decimal,
    Date,
    Select,
    Lookup,

    /// <summary>Text column with a remote type-ahead: the posted value is the typed text.</summary>
    Suggest,
    Toggle,
    ReadOnly,
    Computed,
    Hidden,
}
