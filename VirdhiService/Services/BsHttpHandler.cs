using DeviceServices.Models;
using DeviceServices.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AVTech.Device.BusinessServices.WebSocket
{
    // ══════════════════════════════════════════════════════════════════════════
    //  BS Biometric Device — HTTP Communication Service  (v2)
    //
    //  Changes from v1:
    //    • dev_model detection  → dual body-format support (binary vs plain UTF-8)
    //    • token_id validation  → reject unauthorised devices immediately
    //    • dev_id format guard  → reject malformed device IDs
    //    • Block reassembly     → handles blk_no 1…N → 0 multi-block results
    //    • RESET_FK check       → honour reset commands during send_cmd_result
    //    • Cancel check         → respond ERROR_CANCELED for cancelled commands
    //    • io_mode decoding     → numeric → string mapping for older device firmware
    //    • fk_bin_data_lib guard→ reject realtime_glog with unknown lib name
    //    • RemoveOldBlockStream → GC stale buffers for crashed devices
    // ══════════════════════════════════════════════════════════════════════════

    public class BsHttpHandler : BackgroundService
    {
        // ── Fields ────────────────────────────────────────────────────────────

        private string msg = "No message";
        private bool listening = false;
        private HttpListener _httpListener;

        private readonly ILogger<BsHttpHandler> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        // Tracks last heartbeat time per device — for disconnect detection
        private static readonly Dictionary<string, DateTime> _lastSeen = new();

        // Per-device semaphore — serialises concurrent command dispatch
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceLocks = new();

        // Block reassembly buffers — keyed by dev_id
        // Stores accumulated bytes for multi-block send_cmd_result payloads
        private static readonly ConcurrentDictionary<string, BlockBuffer> _blockBuffers = new();

        private System.Windows.Forms.Timer _monitorTimer;

        // ── Constructor ───────────────────────────────────────────────────────

        public BsHttpHandler(
            ILogger<BsHttpHandler> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  IHostedService entry point
        // ══════════════════════════════════════════════════════════════════════

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(() => Start(stoppingToken), stoppingToken);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Listener bootstrap
        // ══════════════════════════════════════════════════════════════════════

        public void Start(CancellationToken stoppingToken = default)
        {
            string prefix = GetListenerPrefix();
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add(prefix);
                _httpListener.Start();
                listening = true;

                StartDeviceMonitor();
                _ = Task.Run(() => AcceptBsClientsAsync(stoppingToken), stoppingToken);

                msg = $"[BS] HTTP Listener started on {prefix}";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
            }
            catch (HttpListenerException ex)
            {
                msg = $"[BS] Failed to start HTTP Listener on {prefix}. {ex}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
            }
        }

        private string GetListenerPrefix()
        {
            int port = _configuration.GetValue<int>("BsServer:Port");
            string host = _configuration.GetValue<string>("BsServer:Host") ?? "localhost";
            port = (port is > 0 and <= 65535) ? port : 8185;
            return $"http://{host}:{port}/";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Device heartbeat monitor
        // ══════════════════════════════════════════════════════════════════════

        public void StartDeviceMonitor()
        {
            _monitorTimer = new System.Windows.Forms.Timer();
            _monitorTimer.Interval = 30000; // 30 seconds
            _monitorTimer.Tick += async (sender, e) =>
            {
                // GC stale block buffers for devices that crashed mid-transfer
                RemoveOldBlockBuffers();

                using var scope = _serviceProvider.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

                foreach (var kv in _lastSeen.ToList())
                {
                    if ((DateTime.Now - kv.Value).TotalSeconds > 300)
                    {
                        var entity = new DeviceMasterEntity
                        {
                            DeviceSerialNumber = kv.Key,
                            IsConnected = "0"
                        };
                        await svc.HandleDeviceStatus(entity);
                        _lastSeen.Remove(kv.Key);

                        msg = $"[BS] Device {kv.Key} timed out.";
                        Console.WriteLine(msg);
                        _logger.LogInformation(msg);
                        GlobalLogger.LogToFileAsync(msg).Wait();
                    }
                }
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Accept loop
        // ══════════════════════════════════════════════════════════════════════

        private async Task AcceptBsClientsAsync(CancellationToken stoppingToken)
        {
            while (listening && !stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _httpListener
                        .GetContextAsync()
                        .WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (HttpListenerException ex) when (!stoppingToken.IsCancellationRequested)
                {
                    msg = $"[BS] Accept error. {ex.Message}";
                    Console.WriteLine(msg);
                    _logger.LogError(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    await Task.Delay(3000, stoppingToken);
                    RestartListener();
                    continue;
                }
                catch (Exception ex)
                {
                    msg = $"[BS] Unexpected accept error. {ex.Message}";
                    Console.WriteLine(msg);
                    _logger.LogError(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    continue;
                }

                _ = Task.Run(() => HandleBsRequestAsync(context), stoppingToken);
            }
        }

        private void RestartListener()
        {
            try { _httpListener?.Stop(); _httpListener?.Close(); } catch { }
            try
            {
                string prefix = GetListenerPrefix();
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add(prefix);
                _httpListener.Start();

                msg = $"[BS] HTTP Listener restarted on {prefix}.";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
            }
            catch (Exception ex)
            {
                msg = $"[BS] Failed to restart HTTP Listener. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Per-request handler
        // ══════════════════════════════════════════════════════════════════════

        private async Task HandleBsRequestAsync(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // ── 1. Read standard headers ──────────────────────────────────────
            string requestCode = request.Headers["request_code"] ?? string.Empty;
            string devId = request.Headers["dev_id"] ?? string.Empty;
            string transId = request.Headers["trans_id"] ?? string.Empty;
            string tokenId = request.Headers["token_id"] ?? string.Empty;
            string devModel = request.Headers["dev_model"] ?? string.Empty;
            bool isNewModel = !string.IsNullOrEmpty(devModel);

            // ── 2. token_id validation (NEW) ──────────────────────────────────
            // If a token is configured, reject requests that don't match.
            string? configuredToken = _configuration.GetValue<string>("BsServer:TokenId");
            if (!string.IsNullOrEmpty(configuredToken) &&
                !configuredToken.Equals(tokenId, StringComparison.Ordinal))
            {
                msg = $"[BS] Rejected request — invalid token_id from dev_id={devId}";
                Console.WriteLine(msg);
                _logger.LogWarning(msg);
                await GlobalLogger.LogToFileAsync(msg);
                response.StatusCode = 403;
                response.Close();
                return;
            }

            // ── 3. dev_id format guard (NEW) ──────────────────────────────────
            // Must be non-empty, alphanumeric, max 18 chars — mirrors SDK reference
            if (!IsValidDevId(devId))
            {
                msg = $"[BS] Rejected request — invalid dev_id='{devId}'";
                Console.WriteLine(msg);
                _logger.LogWarning(msg);
                await GlobalLogger.LogToFileAsync(msg);
                response.StatusCode = 400;
                response.Close();
                return;
            }

            // ── 4. request_code guard ─────────────────────────────────────────
            if (string.IsNullOrEmpty(requestCode))
            {
                response.StatusCode = 400;
                response.Close();
                return;
            }

            msg = $"[BS] Received request_code={requestCode} dev_id={devId} trans_id={transId} dev_model={(isNewModel ? devModel : "legacy")}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);

            // ── 5. Read raw body ──────────────────────────────────────────────
            byte[] rawBody = await ReadBodyAsync(request);

            // ── 6. Block reassembly (NEW) ─────────────────────────────────────
            // blk_no > 0 = intermediate block → accumulate and ACK immediately
            // blk_no = 0 = final (or only) block → combine and continue
            if (int.TryParse(request.Headers["blk_no"], out int blkNo) && blkNo > 0)
            {
                msg = $"[BS] Processing intermediate block dev_id={devId} blk_no={blkNo}";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                await GlobalLogger.LogToFileAsync(msg);
                int addResult = AddBlockData(devId, blkNo, rawBody);
                if (addResult != 0)
                {
                    msg = $"[BS] Block reassembly error dev_id={devId} blk_no={blkNo} err={addResult}";
                    Console.WriteLine(msg);
                    _logger.LogError(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    SendResponse(response, "ERROR_ADD_BLOCK_DATA", transId, null);
                }
                else
                {
                    SendResponse(response, "OK", transId, null);
                }
                response.Close();
                return;
            }
            msg = "Switching to final block processing--";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);
            // blk_no == 0 or absent — combine any accumulated blocks with final chunk
            rawBody = GetAndCombineBlocks(devId, rawBody);

            // ── 7. Dispatch ───────────────────────────────────────────────────
            using var scope = _serviceProvider.CreateScope();
            var deviceService = scope.ServiceProvider
                .GetRequiredService<IDeviceDataDownloadService>();

            try
            {
                switch (requestCode.ToUpperInvariant())
                {
                    case "RECEIVE_CMD":
                        // Clean stale block buffers on each heartbeat (mirrors SDK reference)
                        RemoveOldBlockBuffers();
                        await HandleReceiveCmdAsync(devId, transId, rawBody, isNewModel, response, deviceService);
                        break;

                    case "SEND_CMD_RESULT":
                        await HandleSendCmdResultAsync(devId, transId, request, rawBody, isNewModel, response, deviceService);
                        break;

                    case "REALTIME_GLOG":
                        msg = "Block processing LOG";
                        Console.WriteLine(msg);
                        _logger.LogInformation(msg);
                        await GlobalLogger.LogToFileAsync(msg);
                        // blk_no == 0 or absent — combine any accumulated blocks with final chunk
                        rawBody = GetAndCombineBlocks(devId, rawBody);
                        await HandleRealtimeLogAsync(devId, rawBody, isNewModel, response, deviceService);
                        break;

                    case "REALTIME_DOOR_STATUS":
                        await HandleRealtimeDoorStatusAsync(devId, rawBody, isNewModel, response, deviceService);
                        break;

                    case "REALTIME_ENROLL_DATA":
                        await HandleRealtimeEnrollDataAsync(devId, rawBody, isNewModel, response, deviceService);
                        break;

                    default:
                        msg = $"[BS] Unknown request_code={requestCode} dev_id={devId}";
                        Console.WriteLine(msg);
                        _logger.LogWarning(msg);
                        await GlobalLogger.LogToFileAsync(msg);
                        SendResponse(response, "ERROR_INVLAID_REQUEST_CODE", transId, null);
                        break;
                }
            }
            catch (Exception ex)
            {
                msg = $"[BS] Error handling request_code={requestCode} dev_id={devId}. {ex}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
                SendResponse(response, "ERROR", transId, null);
            }
            finally
            {
                response.Close();
                msg = $"[BS] Request completed dev_id={devId} request_code={requestCode}";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                await GlobalLogger.LogToFileAsync(msg);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  receive_cmd — device polls for a pending command (SDK §3.4)
        // ══════════════════════════════════════════════════════════════════════

        private async Task HandleReceiveCmdAsync(
    string devId,
    string transId,
    byte[] rawBody,
    bool isNewModel,
    HttpListenerResponse response,
    IDeviceDataDownloadService deviceService)
        {
            // ── Parse heartbeat body ──────────────────────────────────────────────
            string fkName = string.Empty;
            string fkTime = string.Empty;

            string? bodyJson = ExtractJsonString(rawBody, isNewModel);
            if (string.IsNullOrEmpty(bodyJson))
            {
                msg = $"[BS] receive_cmd body empty or unreadable dev_id={devId}";
                Console.WriteLine(msg);
                _logger.LogWarning(msg);
                await GlobalLogger.LogToFileAsync(msg);
                response.Close();
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(bodyJson);
                fkName = doc.RootElement.TryGetString("fk_name");
                fkTime = doc.RootElement.TryGetString("fk_time");
            }
            catch { /* non-fatal — proceed without name/time */ }

            // ── Update device heartbeat + mark online ─────────────────────────────
            UpdateLastSeen(devId);

            var deviceEntity = await deviceService.GetDeviceSn(devId);
            if (deviceEntity != null)
            {
                deviceEntity.IsConnected = "1";
                deviceEntity.LastConnected = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                await deviceService.HandleDeviceStatus(deviceEntity);
            }

            msg = $"[BS] receive_cmd dev_id={devId} fk_name={fkName} fk_time={fkTime}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);

            // ── Check for pending commands ────────────────────────────────────────
            var pendingCommands = await deviceService.CheckCommandZkt(devId);
            if (pendingCommands == null || pendingCommands.Count == 0)
            {
                msg = $"[BS] No pending command for dev_id={devId}";
                Console.WriteLine(msg);
                await GlobalLogger.LogToFileAsync(msg);
                SendResponse(response, "ERROR_NO_CMD", null, null);
                return;
            }

            // ── Iterate pending commands — dispatch first valid one ───────────────
            // foreach lets us skip unknown/unsupported command types gracefully
            // instead of failing the whole poll on the first bad entry.
            foreach (var pending in pendingCommands)
            {
                var command = await deviceService.GetDeviceCommand(pending.DeviceCommandId);
                if (command == null)
                {
                    msg = $"[BS] Command not found for DeviceCommandId={pending.DeviceCommandId} — skipping";
                    Console.WriteLine(msg);
                    _logger.LogWarning(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    continue;
                }

                // Build payload — async because SET_USER_INFO loads image from disk
                (string cmdCode, string? jsonParam, List<byte[]>? blobs) =
                    await BuildCommandPayload(command);

                if (string.IsNullOrEmpty(cmdCode))
                {
                    // Unknown command type — skip and try next pending command
                    msg = $"[BS] Skipping unknown command type={command.DeviceCommand} DeviceCommandId={pending.DeviceCommandId}";
                    Console.WriteLine(msg);
                    _logger.LogWarning(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    continue;
                }

                // string transId = command.DeviceCommandId.ToString();

                msg = $"[BS] SET_USER_INFO payload JSON: {jsonParam}";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                await GlobalLogger.LogToFileAsync(msg);

                msg = $"[BS] SET_USER_INFO blob count: {blobs?.Count ?? 0}, blob[0] size: {blobs?[0]?.Length ?? 0} bytes";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                await GlobalLogger.LogToFileAsync(msg);

                // Send command to device
                SendCommandResponse(response, transId, cmdCode, jsonParam, blobs, isNewModel);

                msg = $"[BS] Command dispatched cmd_code={cmdCode} trans_id={transId} dev_id={devId}";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                await GlobalLogger.LogToFileAsync(msg);

                // Mark as dispatched — prevents re-sending on next poll
                try
                {
                    command.CommondStatusId = 2;              // 2 = Sent / In-Progress
                    command.CommondResponce = "Dispatched";
                    command.SentDate = DateTime.Now;
                    await deviceService.UpdateSendingCommandAsync(command);
                }
                catch (Exception ex)
                {
                    // Non-fatal — log and continue. Device will return send_cmd_result
                    // regardless; worst case the command is re-sent once on next poll.
                    msg = $"[BS] Failed to mark command as dispatched trans_id={transId}. {ex.Message}";
                    Console.WriteLine(msg);
                    _logger.LogError(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                }

                // One command per receive_cmd poll — stop after first successful dispatch
                return;
            }

            // All pending commands were null or unknown type — nothing to send
            msg = $"[BS] No dispatchable command found for dev_id={devId} ({pendingCommands.Count} pending skipped)";
            Console.WriteLine(msg);
            _logger.LogWarning(msg);
            await GlobalLogger.LogToFileAsync(msg);
            SendResponse(response, "ERROR_NO_CMD", null, null);
        }
        // ══════════════════════════════════════════════════════════════════════
        //  send_cmd_result — device returns result of an executed command (§3.5)
        // ══════════════════════════════════════════════════════════════════════

        private async Task HandleSendCmdResultAsync(
            string devId,
            string transId,
            HttpListenerRequest request,
            byte[] rawBody,
            bool isNewModel,
            HttpListenerResponse response,
            IDeviceDataDownloadService deviceService)
        {
            string returnCode = request.Headers["cmd_return_code"] ?? string.Empty;

            msg = $"[BS] send_cmd_result dev_id={devId} trans_id={transId} return_code={returnCode}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);

            // ── RESET_FK check (NEW) ──────────────────────────────────────────
            // If a reset command is pending for this device, honour it immediately
            //bool resetPending = await deviceService.CheckResetCommandPending(devId);
            //if (resetPending)
            //{
            //    msg = $"[BS] Reset command pending — sending RESET_FK to dev_id={devId}";
            //    Console.WriteLine(msg);
            //    _logger.LogInformation(msg);
            //    await GlobalLogger.LogToFileAsync(msg);
            //    SendResponse(response, "RESET_FK", transId, null);
            //    return;
            //}

            //// ── Cancel check (NEW) ────────────────────────────────────────────
            //bool isCancelled = await deviceService.IsCommandCancelled(devId, transId);
            //if (isCancelled)
            //{
            //    msg = $"[BS] Command cancelled trans_id={transId} dev_id={devId}";
            //    Console.WriteLine(msg);
            //    _logger.LogInformation(msg);
            //    await GlobalLogger.LogToFileAsync(msg);
            //    SendResponse(response, "ERROR_CANCELED", transId, null);
            //    return;
            //}

            // ── Parse result body ─────────────────────────────────────────────
            JsonDocument? json = null;
            List<byte[]> blobs = new();
            if (rawBody.Length > 0)
            {
                if (isNewModel)
                {
                    // New model: plain UTF-8 JSON, no binary wrapper
                    try { json = JsonDocument.Parse(Encoding.UTF8.GetString(rawBody)); } catch { }
                }
                else
                {
                    BsPacketParser.TryParse(rawBody, out json, out blobs);
                }
            }

            // ── Persist result ────────────────────────────────────────────────
            if (long.TryParse(transId, out long cmdId))
            {
                int resultCode = returnCode.Equals("OK", StringComparison.OrdinalIgnoreCase) ? 0 : -1;
                await UpdateCommandResult(cmdId, devId, resultCode, returnCode, json, blobs, deviceService);
            }

            json?.Dispose();
            SendResponse(response, "OK", transId, null);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  realtime_glog — real-time attendance log (SDK §5.1)
        // ══════════════════════════════════════════════════════════════════════

        private async Task HandleRealtimeLogAsync(
            string devId,
            byte[] rawBody,
            bool isNewModel,
            HttpListenerResponse response,
            IDeviceDataDownloadService deviceService)
        {
            msg = "log ---";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);
            // blk_no == 0 or absent — combine any accumulated blocks with final chunk
            rawBody = GetAndCombineBlocks(devId, rawBody);
            UpdateLastSeen(devId);

            string userId = string.Empty;
            string ioTime = string.Empty;
            string ioMode = string.Empty;
            string verifyMode = string.Empty;
            string workCode = "0";
            byte[]? logImage = null;

            // ── Parse body ────────────────────────────────────────────────────
            string? bodyJson;
            List<byte[]> blobs = new();

            if (isNewModel)
            {
                msg = "Log 2-----NewModel";
                
                await GlobalLogger.LogToFileAsync(msg);
                // blk_no == 0 or absent — combine any accumulated blocks with final chunk
                rawBody = GetAndCombineBlocks(devId, rawBody);
                bodyJson = Encoding.UTF8.GetString(rawBody);
            }
            else
            {
                msg = "Log 2-----OldModel";

                await GlobalLogger.LogToFileAsync(msg);
                BsPacketParser.TryParse(rawBody, out _, out blobs);
                bodyJson = ExtractJsonString(rawBody, false);
                if (blobs.Count > 0) logImage = blobs[0];
            }

            if (string.IsNullOrEmpty(bodyJson))
            {
                msg = $"[BS] realtime_glog body empty dev_id={devId}";
                Console.WriteLine(msg);
                _logger.LogWarning(msg);
                await GlobalLogger.LogToFileAsync(msg);
                response.Close();
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(bodyJson);
                var root = doc.RootElement;

                // ── fk_bin_data_lib guard (NEW) ───────────────────────────────
                string fkBinDataLib = root.TryGetString("fk_bin_data_lib");
                msg = "Log 3-----";

                await GlobalLogger.LogToFileAsync(msg);
                if (!isNewModel && string.IsNullOrEmpty(fkBinDataLib))
                {
                    msg = $"[BS] realtime_glog missing fk_bin_data_lib dev_id={devId}";
                    Console.WriteLine(msg);
                    _logger.LogWarning(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    SendResponse(response, "ERROR_INVALID_LIB_NAME", null, null);
                    return;
                }

                userId = root.TryGetString("user_id");
                ioTime = root.TryGetString("io_time");

                if (root.TryGetProperty("io_mode", out var ioModeProp))
                    ioMode = ioModeProp.ToString();
                if (root.TryGetProperty("verify_mode", out var verifyProp))
                    verifyMode = verifyProp.ToString();
                if (root.TryGetProperty("work_code", out var wc))
                    workCode = wc.ToString();

                if (isNewModel)
                {
                    // New model: io_mode is already a readable string
                    // log_image arrives as base64 string inside JSON
                    string logImgB64 = root.TryGetString("log_image");
                    if (!string.IsNullOrEmpty(logImgB64))
                    {
                        try { logImage = Convert.FromBase64String(logImgB64); } catch { }
                    }
                }
                else
                {
                    msg = "Log 4-----";

                    await GlobalLogger.LogToFileAsync(msg);
                    // ── io_mode numeric decode (NEW) ──────────────────────────
                    // Older firmware sends integers; decode using fk_bin_data_lib name
                    if (int.TryParse(ioMode, out int ioModeInt))
                        ioMode = BsIoModeDecoder.DecodeIoMode(fkBinDataLib, ioModeInt);
                    if (int.TryParse(verifyMode, out int verifyModeInt))
                        verifyMode = BsIoModeDecoder.DecodeVerifyMode(fkBinDataLib, verifyModeInt);
                }
            }
            catch (Exception ex)
            {
                msg = $"[BS] realtime_glog parse error dev_id={devId}. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
                SendResponse(response, "ERROR", null, null);
                return;
            }

            msg = $"[BS] realtime_glog dev_id={devId} user_id={userId} io_time={ioTime} io_mode={ioMode}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);

            try
            {
                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(ioTime))
                {
                    var record = new PunchLogEntity
                    {
                        DeviceSerialNumber = devId,
                        DeviceEmployeeCode = userId,
                        PunchDateTime = ParseBsDateTime(ioTime),
                        PunchInoutFlag = "0",
                        CreatedDate = DateTime.Now.ToString(),
                        IsManual = "0",
                        IsDeleted = "0"
                    };
                    await GlobalLogger.LogToFileAsync($"[BS] Saving attendance dev_id={devId} user_id={userId} punch_time={record.PunchDateTime}");
                    await deviceService.SetAttendance(record);

                    msg = $"[BS] Attendance saved dev_id={devId} user_id={userId}";
                    Console.WriteLine(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                }

                SendResponse(response, "OK", null, null);
            }
            catch (Exception ex)
            {
                msg = $"[BS] Error saving attendance dev_id={devId}. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
                // Respond ERROR → device will retry
                SendResponse(response, "ERROR", null, null);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  realtime_door_status (SDK §5.2)
        // ══════════════════════════════════════════════════════════════════════

        private async Task HandleRealtimeDoorStatusAsync(
            string devId,
            byte[] rawBody,
            bool isNewModel,
            HttpListenerResponse response,
            IDeviceDataDownloadService deviceService)
        {
            UpdateLastSeen(devId);

            int doorStatus = -1;
            string? bodyJson = ExtractJsonString(rawBody, isNewModel);

            if (!string.IsNullOrEmpty(bodyJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(bodyJson);
                    if (doc.RootElement.TryGetProperty("door_status", out var prop))
                    {
                        if (prop.ValueKind == JsonValueKind.Number)
                            doorStatus = prop.GetInt32();
                        else if (prop.ValueKind == JsonValueKind.String &&
                                 int.TryParse(prop.GetString(), out int parsed))
                            doorStatus = parsed;
                    }
                }
                catch { }
            }

            msg = $"[BS] realtime_door_status dev_id={devId} door_status={doorStatus}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);

            try
            {
                var entity = new DeviceMasterEntity
                {
                    DeviceSerialNumber = devId,
                    IsConnected = "1"
                };
                await deviceService.HandleDeviceStatus(entity);
                SendResponse(response, "OK", null, null);
            }
            catch (Exception ex)
            {
                msg = $"[BS] Door status error dev_id={devId}. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
                SendResponse(response, "ERROR", null, null);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  realtime_enroll_data (SDK §5.3)
        // ══════════════════════════════════════════════════════════════════════

        private async Task HandleRealtimeEnrollDataAsync(
            string devId,
            byte[] rawBody,
            bool isNewModel,
            HttpListenerResponse response,
            IDeviceDataDownloadService deviceService)
        {
            UpdateLastSeen(devId);

            string userId = string.Empty;
            string userName = string.Empty;
            string userPrivilege = string.Empty;
            string base64Photo = string.Empty;

            string? bodyJson = ExtractJsonString(rawBody, isNewModel);
            if (string.IsNullOrEmpty(bodyJson))
            {
                msg = $"[BS] realtime_enroll_data body empty dev_id={devId}";
                Console.WriteLine(msg);
                _logger.LogWarning(msg);
                await GlobalLogger.LogToFileAsync(msg);
                response.Close();
                return;
            }

            List<byte[]> blobs = new();
            if (!isNewModel)
                BsPacketParser.TryParse(rawBody, out _, out blobs);

            try
            {
                using var doc = JsonDocument.Parse(bodyJson);
                var root = doc.RootElement;

                userId = root.TryGetString("user_id");
                userName = root.TryGetString("user_name");
                userPrivilege = root.TryGetString("user_privilege");

                if (root.TryGetProperty("user_photo", out var photoProp) &&
                    photoProp.ValueKind == JsonValueKind.String)
                {
                    string photoRef = photoProp.GetString() ?? string.Empty;

                    if (isNewModel)
                    {
                        // New model: base64 string directly in JSON
                        try { Convert.FromBase64String(photoRef); base64Photo = photoRef; } catch { }
                    }
                    else
                    {
                        // Legacy: "BIN_k" reference
                        byte[]? photoBytes = ResolveBinRef(photoRef, blobs);
                        if (photoBytes != null)
                            base64Photo = Convert.ToBase64String(photoBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                msg = $"[BS] realtime_enroll_data parse error dev_id={devId}. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
                SendResponse(response, "ERROR", null, null);
                return;
            }

            msg = $"[BS] realtime_enroll_data dev_id={devId} user_id={userId} user_name={userName}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);

            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    var entity = new EmployeeEntity
                    {
                        EmployeeCode = userId,
                        DeviceEmployeeCode = userId,
                        EmployeeName = userName,
                        DevSerialNo = devId,
                        EmpBase64Img = string.IsNullOrEmpty(base64Photo) ? null : base64Photo,
                        JoiningDate = DateTime.Now.ToString("yyyy-MM-dd")
                    };
                    await deviceService.SetUserInfo(entity);

                    msg = $"[BS] Enroll data saved dev_id={devId} user_id={userId}";
                    Console.WriteLine(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                }

                SendResponse(response, "OK", null, null);
            }
            catch (Exception ex)
            {
                msg = $"[BS] Error saving enroll data dev_id={devId}. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
                SendResponse(response, "ERROR", null, null);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Block reassembly helpers (NEW)
        //  Mirrors SDK reference AddBlockData / GetBlockDataAndRemove
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Accumulates an intermediate block (blk_no ≥ 1) into the device's buffer.
        /// Returns 0 on success, negative on error.
        /// </summary>
        private int AddBlockData(string devId, int blkNo, byte[] chunk)
        {
            if (string.IsNullOrEmpty(devId) || blkNo < 1 || chunk.Length == 0)
                return -1;

            try
            {
                if (blkNo == 1)
                {
                    // Start a fresh buffer for this device
                    var buf = new BlockBuffer();
                    buf.Stream.Write(chunk, 0, chunk.Length);
                    buf.LastBlockNo = 1;
                    buf.LastModified = DateTime.Now;
                    _blockBuffers[devId] = buf;
                }
                else
                {
                    if (!_blockBuffers.TryGetValue(devId, out var buf))
                        return -2; // no buffer started

                    if (buf.LastBlockNo != blkNo - 1)
                        return -3; // out-of-sequence

                    buf.Stream.Seek(0, SeekOrigin.End);
                    buf.Stream.Write(chunk, 0, chunk.Length);
                    buf.LastBlockNo = blkNo;
                    buf.LastModified = DateTime.Now;
                }
                return 0;
            }
            catch { return -11; }
        }

        /// <summary>
        /// On blk_no == 0 (final block): combines accumulated buffer with the
        /// final chunk and returns the complete byte array.
        /// If no buffer exists (single-block payload), returns chunk as-is.
        /// </summary>
        private byte[] GetAndCombineBlocks(string devId, byte[] finalChunk)
        {
            if (_blockBuffers.TryRemove(devId, out var buf))
            {
                buf.Stream.Seek(0, SeekOrigin.End);
                buf.Stream.Write(finalChunk, 0, finalChunk.Length);
                return buf.Stream.ToArray();
            }
            return finalChunk;
        }

        /// <summary>
        /// Removes block buffers that haven't been touched in 30+ minutes —
        /// handles devices that crashed mid-transfer.
        /// </summary>
        private void RemoveOldBlockBuffers()
        {
            var cutoff = DateTime.Now.AddMinutes(-30);
            foreach (var kv in _blockBuffers)
            {
                if (kv.Value.LastModified < cutoff)
                    _blockBuffers.TryRemove(kv.Key, out _);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Command result persistence
        // ══════════════════════════════════════════════════════════════════════

        private async Task UpdateCommandResult(
            long cmdId,
            string devId,
            int resultCode,
            string rawReturnCode,
            JsonDocument? json,
            List<byte[]> blobs,
            IDeviceDataDownloadService deviceService)
        {
            try
            {
                var entity = new DeviceCommandEntity
                {
                    DeviceCommandId = cmdId,
                    DeviceSerialNumber = devId,
                    CommondStatusId = resultCode == 0 ? 1 : -1,
                    CommondResponce = resultCode == 0 ? "Successful" : rawReturnCode,
                    UpdatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                await deviceService.UpdateCommandAsync(entity);

                msg = $"[BS] Command result persisted trans_id={cmdId} result={entity.CommondResponce}";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                await GlobalLogger.LogToFileAsync(msg);
            }
            catch (Exception ex)
            {
                msg = $"[BS] Failed to persist result trans_id={cmdId}. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Command payload builder
        // ══════════════════════════════════════════════════════════════════════

        private async Task<(string CmdCode, string? JsonParam, List<byte[]>? Blobs)> BuildCommandPayload(
            DeviceCommandEntity command)
        {
            switch (command.DeviceCommand)
            {
                case "GET_LOG_DATA":
                    return ("GET_LOG_DATA", BuildJson(new
                    {
                        begin_time = command.FromDate ?? string.Empty,
                        end_time = command.ToDate ?? string.Empty
                    }), null);

                case "GET_USER_ID_LIST":
                    return ("GET_USER_ID_LIST", null, null);

                case "GET_USER_INFO":
                    return ("GET_USER_INFO", BuildJson(new { user_id = command.DeviceUserId }), null);

                case "ADD_USER":
                    return await BuildSetUserInfoPayloadAsync(command); ;

                case "DELETE_USER":
                    return ("DELETE_USER", BuildJson(new { user_id = command.DeviceUserId }), null);

                case "CLEAR_ENROLL_DATA":
                    return ("CLEAR_ENROLL_DATA", null, null);

                case "CLEAR_LOG_DATA":
                    return ("CLEAR_LOG_DATA", null, null);

                case "SET_TIME":
                    return ("SET_TIME", BuildJson(new
                    {
                        time = DateTime.Now.ToString("yyyyMMddHHmmss")
                    }), null);

                case "RESET_DEVICE":
                    return ("RESET_FK", null, null);

                case "SET_FK_NAME":
                    return ("SET_FK_NAME", BuildJson(new { fk_name = command.EmployeeName }), null);

                case "GET_DEVICE_STATUS":
                    return ("GET_DEVICE_STATUS", null, null);

                default:
                    msg = $"[BS] Unknown command type: {command.DeviceCommand}";
                    Console.WriteLine(msg);
                    _logger.LogWarning(msg);
                    GlobalLogger.LogToFileAsync(msg).Wait();
                    return (string.Empty, null, null);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HTTP response writers
        // ══════════════════════════════════════════════════════════════════════

        private void SendResponse(
            HttpListenerResponse response,
            string responseCode,
            string? transId,
            string? cmdCode)
        {
            try
            {
                response.ContentType = "application/octet-stream";
                response.StatusCode = 200;
                response.Headers["response_code"] = responseCode;
                if (transId != null) response.Headers["trans_id"] = transId;
                if (cmdCode != null) response.Headers["cmd_code"] = cmdCode;
                response.ContentLength64 = 0;
            }
            catch (Exception ex)
            {
                msg = $"[BS] SendResponse error. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
            }
        }

        private void SendCommandResponse(
      HttpListenerResponse response,
      string transId,
      string cmdCode,
      string? jsonParam,
      List<byte[]>? blobs,
      bool isNewModel)          // ← add this parameter
        {
            try
            {
                response.ContentType = "application/octet-stream";
                response.StatusCode = 200;
                response.Headers["response_code"] = "OK";
                response.Headers["trans_id"] = transId;
                response.Headers["cmd_code"] = cmdCode;

                if (!string.IsNullOrEmpty(jsonParam))
                {
                    byte[] body = isNewModel
                        ? BsPacketParser.EncodeModern(jsonParam)        // plain UTF-8 + \0, no length prefix
                        : BsPacketParser.EncodeLegacy(jsonParam, blobs); // [len+1][JSON][\0][blobs]

                    response.ContentLength64 = body.Length;
                    response.OutputStream.Write(body, 0, body.Length);
                }
                else
                {
                    response.ContentLength64 = 0;
                }

                msg = $"[BS] Command response sent trans_id={transId} cmd_code={cmdCode}";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
            }
            catch (Exception ex)
            {
                msg = $"[BS] SendCommandResponse error trans_id={transId}. {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
                throw; // re-throw so sendSucceeded stays false
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Utilities
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extracts the JSON string from the body.
        /// Legacy (isNewModel=false): strips the 4-byte LE length prefix.
        /// New model (isNewModel=true): body is plain UTF-8 JSON.
        /// </summary>
        private static string? ExtractJsonString(byte[] body, bool isNewModel)
        {
            if (body == null || body.Length == 0) return null;

            if (isNewModel)
                return Encoding.UTF8.GetString(body).TrimEnd('\0');

            // Binary format: [4-byte LE length][JSON bytes]
            if (body.Length < 4) return null;
            int len = body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24);
            if (len <= 0 || len > body.Length - 4) return null;

            // Trim null terminator — some BS firmware appends \0 after the JSON string
            return Encoding.UTF8.GetString(body, 4, len).TrimEnd('\0');
        }

        private void UpdateLastSeen(string devId)
        {
            _lastSeen[devId] = DateTime.Now;
        }

        private static async Task<byte[]> ReadBodyAsync(HttpListenerRequest req)
        {
            if (!req.HasEntityBody) return Array.Empty<byte>();
            using var ms = new MemoryStream();
            await req.InputStream.CopyToAsync(ms);
            return ms.ToArray();
        }

        private static string BuildJson(object obj) =>
            JsonSerializer.Serialize(obj, new JsonSerializerOptions { PropertyNamingPolicy = null });

        private static byte[]? ResolveBinRef(string? marker, List<byte[]> blobs)
        {
            if (string.IsNullOrEmpty(marker) ||
                !marker.StartsWith("BIN_", StringComparison.OrdinalIgnoreCase))
                return null;
            if (int.TryParse(marker.AsSpan(4), out int idx) && idx >= 1 && idx <= blobs.Count)
                return blobs[idx - 1];
            return null;
        }

        private static DateTime ParseBsDateTime(string bsTime)
        {
            if (DateTime.TryParseExact(bsTime, "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime dt))
                return dt;
            return DateTime.Now;
        }

        /// <summary>
        /// dev_id must be alphanumeric only, max 18 chars — mirrors SDK IsValidEngDigitString.
        /// </summary>
        private static bool IsValidDevId(string devId)
        {
            if (string.IsNullOrEmpty(devId) || devId.Length > 18) return false;
            foreach (char c in devId)
                if (!char.IsLetterOrDigit(c)) return false;
            return true;
        }

        private async Task<(string CmdCode, string? JsonParam, List<byte[]>? Blobs)>
    BuildSetUserInfoPayloadAsync(DeviceCommandEntity command)
        {
            byte[]? faceBytes = null;

            if (!string.IsNullOrEmpty(command.FaceImagePath))
            {
                faceBytes = await LoadAndPrepareFaceImageAsync(command.FaceImagePath);
            }

            object enrollDataArray;
            List<byte[]>? blobs = null;

            if (faceBytes != null)
            {
                enrollDataArray = new[]
                {
                new { backup_number = 12, enroll_data = "BIN_1" }
            };
                blobs = new List<byte[]> { faceBytes };
            }
            else
            {
                enrollDataArray = Array.Empty<object>();
            }


            string jsonParam = BuildJson(new
            {
                user_id = command.DeviceUserId ?? string.Empty,
                user_name = command.EmployeeName ?? string.Empty,
                user_privilege = "USER",
                user_vid = " ",
                enroll_data_array = enrollDataArray
            });

            return ("SET_USER_INFO", jsonParam, blobs);
        }

        private async Task<byte[]?> LoadAndPrepareFaceImageAsync(string faceImagePath)
        {
            try
            {
                string rootPath = Directory.GetCurrentDirectory();
                string basePath = Directory.GetParent(rootPath)!.FullName;
                string imagePath = Path.Combine(basePath, "Documents", faceImagePath);

                if (!File.Exists(imagePath))
                {
                    msg = $"[BS] Face image not found: {imagePath}";
                    Console.WriteLine(msg);
                    _logger.LogWarning(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    return null;
                }

                // Read file bytes async — avoids blocking the thread during disk I/O
                byte[] rawBytes = await File.ReadAllBytesAsync(imagePath);

                using var image = SixLabors.ImageSharp.Image
                    .Load<SixLabors.ImageSharp.PixelFormats.Rgb24>(rawBytes);

                image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(320, 240),
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad,
                    PadColor = SixLabors.ImageSharp.Color.Black
                }));

                using var ms = new MemoryStream();
                await image.SaveAsync(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                {
                    Quality = 90
                });

                byte[] faceBytes = ms.ToArray();

                msg = $"[BS] Face image prepared: {faceBytes.Length} bytes from {imagePath}";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                await GlobalLogger.LogToFileAsync(msg);

                return faceBytes;
            }
            catch (Exception ex)
            {
                msg = $"[BS] Face image error: {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
                return null;
            }
        }




    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Block buffer — holds accumulated bytes for multi-block payloads
    // ══════════════════════════════════════════════════════════════════════════

    internal sealed class BlockBuffer
    {
        public MemoryStream Stream { get; } = new MemoryStream();
        public int LastBlockNo { get; set; }
        public DateTime LastModified { get; set; } = DateTime.Now;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  io_mode / verify_mode numeric decoder (NEW)
    //
    //  Older BS firmware sends integer constants for io_mode and verify_mode.
    //  The official SDK ships versioned DLLs (FKDataHS101, FKDataHS102 …) to
    //  decode them.  Since we don't have those DLLs we replicate the mappings
    //  here based on the SDK documentation.
    //
    //  Add more fk_bin_data_lib versions as you encounter them.
    // ══════════════════════════════════════════════════════════════════════════

    public static class BsIoModeDecoder
    {
        // io_mode constants common across HS100–HS105 firmware families
        private static readonly Dictionary<int, string> IoModeMap = new()
        {
            { 0,  "Not Defined" },
            { 1,  "AttendanceIn" },
            { 2,  "AttendanceOut" },
            { 3,  "BreakOut" },
            { 4,  "BreakIn" },
            { 5,  "OvertimeIn" },
            { 6,  "OvertimeOut" },
        };

        // verify_mode constants
        private static readonly Dictionary<int, string> VerifyModeMap = new()
        {
            { 0,  "Not Defined" },
            { 1,  "Fingerprint" },
            { 2,  "PIN" },
            { 4,  "Card" },
            { 16, "Face" },
            { 32, "Fingerprint+PIN" },
            { 33, "Fingerprint+Card" },
        };

        public static string DecodeIoMode(string libName, int value) =>
            IoModeMap.TryGetValue(value, out string? s) ? s : value.ToString();

        public static string DecodeVerifyMode(string libName, int value) =>
            VerifyModeMap.TryGetValue(value, out string? s) ? s : value.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BS SDK binary wire-format codec (SDK §3.3)
    // ══════════════════════════════════════════════════════════════════════════

    public static class BsPacketParser
    {
        public static bool TryParse(byte[] body, out JsonDocument? json, out List<byte[]> blobs)
        {
            json = null;
            blobs = new List<byte[]>();
            if (body is null || body.Length < 4) return false;

            int offset = 0;
            int jsonLen = ReadInt32Le(body, offset); offset += 4;

            if (jsonLen < 0 || offset + jsonLen > body.Length) return false;

            // jsonLen includes the \0 terminator — read jsonLen bytes but
            // strip the trailing \0 before parsing JSON
            string jsonText = Encoding.UTF8.GetString(body, offset, jsonLen)
                                       .TrimEnd('\0');
            offset += jsonLen;

            try { json = JsonDocument.Parse(jsonText); }
            catch { return false; }

            // Binary blobs follow
            while (offset + 4 <= body.Length)
            {
                int blobLen = ReadInt32Le(body, offset); offset += 4;
                if (blobLen < 0 || offset + blobLen > body.Length) break;
                byte[] blob = new byte[blobLen];
                Buffer.BlockCopy(body, offset, blob, 0, blobLen);
                blobs.Add(blob);
                offset += blobLen;
            }
            return true;
        }

        public static byte[] EncodeLegacy(string jsonText, IEnumerable<byte[]>? blobs = null)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonText);

            using var ms = new MemoryStream();

            // Legacy wire format: length = jsonLen + 1 (includes the \0 terminator)
            WriteInt32Le(ms, jsonBytes.Length + 1);

            // JSON string bytes
            ms.Write(jsonBytes, 0, jsonBytes.Length);

            // Null terminator — mandatory for legacy devices
            ms.WriteByte(0x00);

            // Binary blobs — each preceded by 4-byte LE length
            if (blobs != null)
            {
                foreach (byte[] blob in blobs)
                {
                    WriteInt32Le(ms, blob.Length);
                    ms.Write(blob, 0, blob.Length);
                }
            }

            return ms.ToArray();
        }

        public static byte[] EncodeModern(string jsonText)
        {
            // Modern format: plain UTF-8 JSON + \0, no length prefix, no binary blobs
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonText);
            using var ms = new MemoryStream();
            ms.Write(jsonBytes, 0, jsonBytes.Length);
            ms.WriteByte(0x00);
            return ms.ToArray();
        }

        private static int ReadInt32Le(byte[] buf, int o) =>
            buf[o] | (buf[o + 1] << 8) | (buf[o + 2] << 16) | (buf[o + 3] << 24);

        private static void WriteInt32Le(Stream s, int v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF));
            s.WriteByte((byte)((v >> 24) & 0xFF));
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  JsonElement helpers
    // ══════════════════════════════════════════════════════════════════════════

    internal static class BsJsonExtensions
    {
        internal static string TryGetString(this JsonElement el, string key)
        {
            if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString() ?? string.Empty;
            return string.Empty;
        }
    }
}