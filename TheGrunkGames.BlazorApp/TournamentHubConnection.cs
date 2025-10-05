using Microsoft.AspNetCore.SignalR.Client;

namespace TheGrunkGames.BlazorApp;

public class TournamentHubConnection : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public event Action? OnTournamentUpdated;

    public TournamentHubConnection(HubConnection connection)
    {
        _connection = connection;

        _connection.On("TournamentUpdated", () =>
        {
            OnTournamentUpdated?.Invoke();
        });

        _connection.Reconnected += _ =>
        {
            OnTournamentUpdated?.Invoke();
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync()
    {
        if (_connection.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _connection.StartAsync();
            }
            catch (Exception)
            {
                // Connection will retry via WithAutomaticReconnect
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
