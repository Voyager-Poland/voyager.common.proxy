namespace Voyager.Common.Proxy.Diagnostics.ApplicationInsights
{
	/// <summary>
	/// Options for configuring Application Insights diagnostics.
	/// </summary>
	public class ApplicationInsightsOptions
	{
		/// <summary>
		/// Gets or sets the Cloud.RoleName to set on all telemetry items.
		/// Useful for distinguishing between different services/environments in Application Insights.
		/// </summary>
		public string? CloudRoleName { get; set; }
	}
}
