namespace ShipSimulator.UI
{
    public readonly struct LocalizedMessage
    {
        private readonly string template;
        private readonly object[] arguments;
        public LocalizedMessage(string template, params object[] arguments)
        {
            this.template = template;
            this.arguments = arguments;
        }
        public override string ToString()
        {
            if (template == null) return string.Empty;
            var displayArguments = new object[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
                displayArguments[i] = arguments[i] is string source ? Localization.Source(source) : arguments[i];
            return Localization.FormatSource(template, displayArguments);
        }
    }
}
