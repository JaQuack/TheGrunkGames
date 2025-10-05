using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TheGrunkGames.Models.TournamentModels;
using TheGrunkGames.Services;

namespace TheGrunkGames.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GameController : ControllerBase
    {
        private readonly IGameService _gameService;

        public GameController(IGameService gameService)
        {
            _gameService = gameService;
        }

        [HttpGet("GetCurrentRound")]
        public async Task<Round?> GetCurrentRound()
        {
            return await _gameService.GetCurrentRound();
        }

        [HttpPost("GetNextRound")]
        public async Task<Round> GetNextRound()
        {
            return await _gameService.GetNextRound();
        }

        [HttpPost("GetNextRound_Staging")]
        public async Task<Round> GetNextRoundStaging()
        {
            return await _gameService.GetNextRoundStaging();
        }

        [HttpPost("SetStaging")]
        public async Task<Round?> SetStaging(bool activate, int roundId)
        {
            return await _gameService.ActivateOrDiscardStaging(activate, roundId);
        }

        [HttpGet("GetRound")]
        public async Task<Round?> GetRound(int roundId)
        {
            var round = await _gameService.GetRound(roundId);

            if (round == null)
            {
                Response.StatusCode = 404;
                return null;
            }

            return round;
        }

        [HttpPost("SetRound_FullOverride")]
        public async Task<IActionResult> SetRound(Round round)
        {
            if (await _gameService.GetRound(round.RoundId) == null)
                return BadRequest();

            await _gameService.SetRound(round);
            return Ok();
        }

        [HttpPost("CompleteMatch")]
        public async Task<IActionResult> CompleteMatch(MatchResult result)
        {
            if (await _gameService.GetMatch(result.MatchId) == null)
                return BadRequest();

            await _gameService.CompleteMatch(result);
            return Ok();
        }


        [HttpPost("Tournament")]
        public async Task<IActionResult> SetTournament(Tournament tournament)
        {
            await _gameService.SetTournament(tournament);
            return Ok();
        }

        [HttpGet("Tournament")]
        public async Task<Tournament> GetTournament()
        {
            var tournament = await _gameService.GetTournament();
            return tournament;
        }

        [HttpPost("Tournament/Reset")]
        public async Task<IActionResult> ResetTournament()
        {
            await _gameService.ResetTournament();
            return Ok();
        }

        [HttpGet("Tournament/History")]
        public async Task<List<TournamentHistorySummary>> GetTournamentHistory()
        {
            return await _gameService.ListTournamentHistory();
        }

        [HttpPost("Tournament/Restore")]
        public async Task<IActionResult> RestoreTournament(string version, string year)
        {
            if (string.IsNullOrEmpty(version))
                return BadRequest("Version is required.");

            await _gameService.GetAndSetHistory(version, year);
            return Ok();
        }

        [HttpGet("Teams")]
        public async Task<List<Team>> GetTeams()
        {
            var teams = (await _gameService.GetTournament()).GetTeams();
            return teams;
        }

        [HttpPost("Teams")]
        public async Task<IActionResult> SetTeams(List<Team> teams)
        {
            await _gameService.SetTeams(teams);
            return Ok();
        }

        [HttpPost("Team")]
        public async Task<IActionResult> AddTeam(Team team)
        {
            await _gameService.AddTeam(team);
            return Ok();
        }

        [HttpDelete("Team")]
        public async Task<IActionResult> RemoveTeam(string teamName)
        {
            if (string.IsNullOrEmpty(teamName))
                return BadRequest("Team name is required.");

            var removed = await _gameService.RemoveTeam(teamName);
            if (!removed)
                return BadRequest("Team not found or is referenced in an active match.");
            return Ok();
        }

        [HttpGet("Games")]
        public async Task<List<Game>> GetGames()
        {
            var games = (await _gameService.GetTournament()).Games;
            return games;
        }

        [HttpPost("Games")]
        public async Task<IActionResult> SetGames(List<Game> games)
        {
            await _gameService.SetGames(games);
            return Ok();
        }

        [HttpPost("Game")]
        public async Task<IActionResult> AddGame(Game game)
        {
            await _gameService.AddGame(game);
            return Ok();
        }

        [HttpDelete("Game")]
        public async Task<IActionResult> RemoveGame(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
                return BadRequest("Game name is required.");

            var removed = await _gameService.RemoveGame(gameName);
            if (!removed)
                return BadRequest("Game not found or is referenced in an active match.");
            return Ok();
        }


        [HttpGet("GetTeamStandings")]
        public async Task<List<TeamStanding>> GetTeamStandings()
        {
            return await _gameService.GetTeamStandings();
        }

        [HttpGet("TeamStats")]
        public async Task<List<TeamStats>> GetTeamStats()
        {
            return await _gameService.GetTeamStats();
        }

        [HttpPost("AddExtraPoints")]
        public async Task<IActionResult> AddExtraPoints(string teamName, int points)
        {
            if (! await _gameService.TeamExists(teamName))
                return BadRequest("No team with that name exists");
            await _gameService.AddExtraPoints(teamName, points);
            return Ok();
        }

        [HttpPost("ChangeGameForMatch")]
        public async Task<IActionResult> ChangeGameForMatch(int matchId, string gameName)
        {
            if (matchId == 0 || string.IsNullOrEmpty(gameName) || !await _gameService.ChangeGameForMatch(matchId, gameName))
                return BadRequest();
            return Ok();
        }

        [HttpPost("ChangeTeamsForMatch")]
        public async Task<IActionResult> ChangeTeamsForMatch(int matchId, string team1Name, string team2Name)
        {
            if (await _gameService.GetMatch(matchId) == null || string.IsNullOrEmpty(team1Name) || string.IsNullOrEmpty(team2Name))
                return BadRequest();
            await _gameService.ChangeTeamsForMatch(matchId, team1Name, team2Name);
            return Ok();
        }
    }
}
