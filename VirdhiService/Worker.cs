using DeviceServices.Services;

namespace DeviceServices
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly WebSocketServerManage _webSocketServer;
        private readonly ZktTCPHandler _zktTcpHandler;
        private readonly EsslTCPHandler _esslTcpHandler;

        public Worker(
            ILogger<Worker> logger,
            ZktTCPHandler zktTcpHandler,
            EsslTCPHandler esslTcpHandler)
        {
            _logger = logger;
            _zktTcpHandler = zktTcpHandler;
            _esslTcpHandler = esslTcpHandler;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting Device Services...");

                //_webSocketServer.Start();
                _zktTcpHandler.Start();
                //_esslTcpHandler.Start();

                await GlobalLogger.LogToFileAsync("Started WebSocket and TCP Services.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Service running at {time}", DateTime.Now);

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker failed.");
                await GlobalLogger.LogToFileAsync(ex.ToString());
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Device Services...");

            // Stop services if Stop() methods exist
            // _webSocketServer.Stop();
            // _zktTcpHandler.Stop();
            // _esslTcpHandler.Stop();

            await base.StopAsync(cancellationToken);
        }
    }
}