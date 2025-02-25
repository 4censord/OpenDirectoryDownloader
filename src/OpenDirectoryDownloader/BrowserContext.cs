﻿using NLog;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace OpenDirectoryDownloader;

public class BrowserContext
{
	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	private const string SetCookieHeader = "set-cookie";
	private const string CloudflareClearanceKey = "cf_clearance";

	private Browser Browser { get; set; }
	private Page Page { get; set; }
	private CookieContainer CookieContainer { get; }
	public bool DebugInfo { get; }
	public TimeSpan Timeout { get; set; }
	private string Url { get; }
	private CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();
	private bool OK { get; set; }

	public BrowserContext(string url, CookieContainer cookieContainer, bool debugInfo = false, TimeSpan timeout = default)
	{
		Url = url;
		CookieContainer = cookieContainer;
		DebugInfo = debugInfo;
		Timeout = timeout;
	}

	public async Task<bool> DoAsync()
	{
		try
		{
			if (Timeout == default)
			{
				Timeout = TimeSpan.FromMinutes(1);
			}

			CancellationTokenSource.CancelAfter(Timeout);

			BrowserFetcher browserFetcher = new BrowserFetcher();

			if (!browserFetcher.LocalRevisions().Contains(BrowserFetcher.DefaultChromiumRevision))
			{
				Logger.Warn($"Downloading browser... First time it can take a while, depending on your internet connection.");
				RevisionInfo revisionInfo = await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
				Logger.Warn($"Downloaded browser. Downloaded: {revisionInfo.Downloaded}, Platform: {revisionInfo.Platform}, Revision: {revisionInfo.Revision}, Path: {revisionInfo.FolderPath}");
			}

			Logger.Debug($"Creating browser...");

			PuppeteerExtra puppeteerExtra = new PuppeteerExtra();

			// Use stealth plugin (needed for Cloudflare / hCaptcha)
			puppeteerExtra.Use(new StealthPlugin());

			using (Browser = await puppeteerExtra.LaunchAsync(new LaunchOptions
			{
				Headless = false,
				Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", $"--user-agent=\"{Constants.UserAgent.Chrome}\"" },
				DefaultViewport = null,
				IgnoreHTTPSErrors = true
			}))
			{
				Logger.Info($"Started browser with PID {Browser.Process.Id}");

				Browser.Closed += Browser_Closed;
				Browser.Disconnected += Browser_Disconnected;
				Browser.TargetChanged += Browser_TargetChanged;
				Browser.TargetCreated += Browser_TargetCreated;
				Browser.TargetDestroyed += Browser_TargetDestroyed;

				Logger.Debug($"Created browser.");

				Logger.Debug($"Creating page...");

				using (Page = (await Browser.PagesAsync())[0])
				{
					Page.Close += Page_Close;
					Page.Console += Page_Console;
					Page.Dialog += Page_Dialog;
					Page.DOMContentLoaded += Page_DOMContentLoaded;
					Page.Error += Page_Error;
					Page.FrameAttached += Page_FrameAttached;
					Page.FrameDetached += Page_FrameDetached;
					Page.FrameNavigated += Page_FrameNavigated;
					Page.Load += Page_Load;
					Page.Metrics += Page_Metrics;
					Page.PageError += Page_PageError;
					Page.Popup += Page_Popup;
					Page.Request += Page_Request;
					Page.RequestFailed += Page_RequestFailed;
					Page.RequestFinished += Page_RequestFinished;
					Page.RequestServedFromCache += Page_RequestServedFromCache;
					Page.Response += Page_Response;
					Page.WorkerCreated += Page_WorkerCreated;
					Page.WorkerDestroyed += Page_WorkerDestroyed;

					Logger.Debug($"Created page.");

					Stopwatch stopwatch = Stopwatch.StartNew();

					Logger.Debug($"Navigating to {Url}..");

					await Page.GoToAsync(Url);
					await Task.Delay(TimeSpan.FromSeconds(60), CancellationTokenSource.Token);

					Logger.Debug($"Navigation done in {stopwatch.ElapsedMilliseconds}ms");
				}
			}

			Logger.Debug("Finished with browser!");
		}
		catch (OperationCanceledException ex)
		{
			if (!OK)
			{
				Logger.Error(ex, "Looks like Cloudflare protection wasn't solved in time.");
			}
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "Error with browser");
		}
		finally
		{
			Logger.Debug("Closing browser");
			await Browser.CloseAsync();
			Logger.Debug("Closed browser");
		}

		return OK;
	}

	private void Browser_Closed(object sender, EventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine("Browser_Closed");
		}
	}

	private void Browser_Disconnected(object sender, EventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine("Browser_Disconnected");
		}
	}

	private void Browser_TargetChanged(object sender, TargetChangedArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Browser_TargetChanged: {e.Target.Url}");
		}
	}

	private void Browser_TargetCreated(object sender, TargetChangedArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Browser_TargetCreated: {e.Target.Url}");
		}
	}

	private void Browser_TargetDestroyed(object sender, TargetChangedArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Browser_TargetDestroyed: {e.Target.Url}");
		}
	}

	private void Page_Close(object sender, EventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_Close");
		}
	}

	private void Page_Console(object sender, ConsoleEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_Console: {e.Message.Type}, {e.Message.Text}");
		}
	}

	private void Page_Dialog(object sender, DialogEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_Dialog: {e.Dialog.DialogType}, {e.Dialog.Message}");
		}
	}

	private void Page_DOMContentLoaded(object sender, EventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine("Page_DOMContentLoaded");
		}
	}

	private void Page_Error(object sender, ErrorEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_Error: {e.Error}");
		}
	}

	private void Page_FrameAttached(object sender, FrameEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_FrameAttached: {e.Frame.Name}, {e.Frame.Url}");
		}
	}

	private void Page_FrameDetached(object sender, FrameEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_FrameDetached: {e.Frame.Name}, {e.Frame.Url}");
		}
	}

	private void Page_FrameNavigated(object sender, FrameEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_FrameNavigated: {e.Frame.Name}, {e.Frame.Url}");
		}
	}

	private void Page_Load(object sender, EventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine("Page_Load");
		}
	}

	private void Page_Metrics(object sender, MetricEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_Metrics: {e.Title}, {e.Metrics.Count}");
		}
	}

	private void Page_PageError(object sender, PageErrorEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_PageError: {e.Message}");
		}
	}

	private void Page_Popup(object sender, PopupEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_Popup: {e.PopupPage.Url}");
		}
	}

	private void Page_Request(object sender, RequestEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_Request: [{e.Request?.Method}] {e.Request?.Url}");
		}
	}

	private void Page_RequestFailed(object sender, RequestEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_RequestFailed: [{e.Request?.Method}] {e.Request?.Url}");
		}
	}

	private void Page_RequestFinished(object sender, RequestEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_RequestFinished: [{e.Request?.Method}] {e.Request?.Url}");
		}
	}

	private void Page_RequestServedFromCache(object sender, RequestEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_RequestServedFromCache: [{e.Request?.Method}] {e.Request?.Url}");
		}
	}

	private void Page_Response(object sender, ResponseCreatedEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_Response: {e.Response.Url}.{Environment.NewLine}Headers: {string.Join(Environment.NewLine, e.Response.Headers.Select(h => $"{h.Key}: {h.Value}"))}");
		}

		if (e.Response.Headers.ContainsKey(SetCookieHeader))
		{
			Uri uri = new Uri(e.Response.Url);
			string baseUrl = $"{uri.Scheme}://{uri.Host}";

			string cookies = e.Response.Headers["set-cookie"];

			if (DebugInfo)
			{
				Console.WriteLine($"Cookies: {cookies}");
			}

			string theCookie = cookies.Split('\n').FirstOrDefault(cookie => cookie.StartsWith(CloudflareClearanceKey));

			if (theCookie != null)
			{
				CookieContainer.SetCookies(new Uri(baseUrl), theCookie);
				Cookie cloudflareClearance = CookieContainer.GetCookies(new Uri(baseUrl)).FirstOrDefault(c => c.Name == CloudflareClearanceKey);

				if (cloudflareClearance != null)
				{
					if (DebugInfo)
					{
						Console.WriteLine($"Cloudflare clearance cookie found: {cloudflareClearance.Value}");
					}

					OK = true;
					CancellationTokenSource.Cancel();
				}
			}
		}
	}

	private void Page_WorkerCreated(object sender, WorkerEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_WorkerCreated: {e.Worker.Url}");
		}
	}

	private void Page_WorkerDestroyed(object sender, WorkerEventArgs e)
	{
		if (DebugInfo)
		{
			Console.WriteLine($"Page_WorkerDestroyed: {e.Worker.Url}");
		}
	}
}
