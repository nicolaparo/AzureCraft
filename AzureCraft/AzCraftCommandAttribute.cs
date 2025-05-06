namespace AzureCraft
{
    public class AzCraftCommandAttribute(string commandName) : Attribute
    {
        public string CommandName { get; } = commandName;
    }
}
