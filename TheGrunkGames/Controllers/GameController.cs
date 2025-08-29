using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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
        private readonly GameService _gameService;

        public GameController(GameService gameService)
        {
            _gameService = gameService;
        }

        [HttpGet("GetCurrentRound")]
        public Round GetCurrentRound()
        {
            return _gameService.GetCurrentRound();
        }

        [HttpGet("GetNextRound")]
        public async Task<Round> GetNextRound()
        {
            return await _gameService.GetNextRound();
        }

        [HttpGet("GetNextRound_Staging")]
        public async Task<Round> GetNextRoundStaging()
        {
            await _gameService.RemoveInactiveRounds();
            //var round = _gameService.GetNextRound();
            var round = await _gameService.GetNextRound();
            round.isStaging = true;
            return round;
        }

        [HttpGet("SetStaging")]
        public async Task<Round> SetStaging(bool activate, int roundId)
        {
            if (roundId == 0)
                throw new Exception("Ivnalid RoundId");

            var round = _gameService.GetRound(roundId);
            if (activate)
            {
                round.isStaging = false;
                await _gameService.RemoveInactiveRounds();
                return round;
            }
            else
            {
                await _gameService.RemoveInactiveRounds();
            }
            return round;
        }

        [HttpGet("GetRound")]
        public Round GetRound(int roundId)
        {
            var round = _gameService.GetRound(roundId);

            if (round == null)
            {
                Response.StatusCode = 404;
                return round;
            }

            return round;
        }

        [HttpPost("SetRound_FullOverride")]
        public async Task<IActionResult> SetRound(Round round)
        {
            if (round == null || _gameService.GetRound(round.RoundId) == null)
                return BadRequest();

            await _gameService.SetRound(round);
            return Ok();
        }

        [HttpPost("CompleteMatch")]
        public async Task<IActionResult> CompleteMatch(MatchResult result)
        {
            if (result == null || _gameService.GetMatch(result.MatchId) == null)
                return BadRequest();

            await _gameService.CompleteMatch(result);
            return Ok();
        }


        [HttpPost("Tournament")]
        public async Task<IActionResult> SetTournament(Tournament tournament)
        {
            if (tournament == null)
                return BadRequest();

            await _gameService.SetTournament(tournament);
            return Ok();
        }

        [HttpGet("Tournament")]
        public Tournament GetTournament()
        {
            var tournament = _gameService.GetTournament();
            return tournament;
        }

        [HttpGet("Teams")]
        public List<Team> GetTeams()
        {
            var teams = _gameService.GetTournament().GetTeams();
            return teams;
        }

        [HttpPost("Teams")]
        public async Task<IActionResult> SetTeams(List<Team> teams)
        {
            if (teams == null)
                return BadRequest();

            await _gameService.SetTeams(teams);
            return Ok();
        }

        [HttpPost("Team")]
        public async Task<IActionResult> AddTeam(Team team)
        {
            if (team == null)
                return BadRequest();

            await _gameService.AddTeam(team);
            return Ok();
        }

        [HttpGet("Games")]
        public List<Game> GetGames()
        {
            var games = _gameService.GetTournament().Games;
            return games;
        }

        [HttpPost("Games")]
        public async Task<IActionResult> SetGames(List<Game> games)
        {
            if (games == null)
                return BadRequest();

            await _gameService.SetGames(games);
            return Ok();
        }

        [HttpPost("Game")]
        public async Task<IActionResult> AddGame(Game game)
        {
            if (game == null)
                return BadRequest();

            await _gameService.AddGame(game);
            return Ok();
        }


        [HttpGet("GetTeamStandings")]
        public List<TeamStanding> GetTeamStandings()
        {
            return _gameService.GetTeamStandings();
        }

        [HttpPost("AddExtraPoints")]
        public async Task<IActionResult> AddExtraPoints(string teamName, int points)
        {
            if (!_gameService.TeamExists(teamName))
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
            if (_gameService.GetMatch(matchId) == null || string.IsNullOrEmpty(team1Name) || string.IsNullOrEmpty(team2Name))
                return BadRequest();
            await _gameService.ChangeTeamsForMatch(matchId, team1Name, team2Name);
            return Ok();
        }
    }
}
