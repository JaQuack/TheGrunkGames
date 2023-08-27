using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.IIS.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGrunkGames2.Objects;
using TheGrunkGames2.Services;

namespace TheGrunkGames2.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GameController : ControllerBase
    {
        private readonly ILogger<GameController> _logger;
        private readonly GameService _gameService;

        public GameController(ILogger<GameController> logger, GameService gameService)
        {
            _logger = logger;
            _gameService = gameService;
        }

        [HttpGet("GetCurrentRound")]
        public string GetCurrentRound()
        {
            var round = _gameService.GetCurrentRound();
            return JsonConvert.SerializeObject(round);
        }

        [HttpGet("GetNextRound")]
        public string GetNextRound()
        {
            var round = _gameService.GetNextRound();
            return JsonConvert.SerializeObject(round);
        }

        [HttpGet("GetNextRound_Staging")]
        public string GetNextRoundStaging()
        {
            _gameService.RemoveInactiveRounds();
            var round = _gameService.GetNextRound();
            round.isStaging = true;
            return JsonConvert.SerializeObject(round);
        }

        [HttpGet("SetStaging")]
        public string SetStaging(bool activate, int roundId)
        {
            if (roundId == 0)
                throw new Exception("Ivnalid RoundId");

            var round = _gameService.GetRound(roundId);
            if (activate)
            {
                round.isStaging = false;
                _gameService.RemoveInactiveRounds();
                return JsonConvert.SerializeObject(round);
            }
            else
            {
                _gameService.RemoveInactiveRounds();
            }
            return JsonConvert.SerializeObject(round);
        }

        [HttpGet("GetRound")]
        public string GetRound(int roundId)
        {
            var round = _gameService.GetRound(roundId);

            if (round == null)
                return "No Round with that Id";

            return JsonConvert.SerializeObject(round);
        }

        [HttpPost("SetRound_FullOverride")]
        public IActionResult SetRound(Round round)
        {
            if (round == null || _gameService.GetRound(round.RoundId) == null)
                return BadRequest();

            _gameService.SetRound(round);
            return Ok();
        }

        [HttpPost("CompleteMatch")]
        public IActionResult CompleteMatch(MatchResult result)
        {
            if (result == null || _gameService.GetMatch(result.MatchId) == null)
                return BadRequest();

            _gameService.CompleteMatch(result);
            return Ok();
        }


        [HttpPost("Tournament")]
        public IActionResult SetTournament(Tournament tournament)
        {
            if (tournament == null)
                return BadRequest();

            _gameService.SetTournament(tournament);
            return Ok();
        }

        [HttpGet("Tournament")]
        public string GetTournament()
        {

            var tournament = _gameService.GetTournament();
            return JsonConvert.SerializeObject(tournament);
        }

        [HttpGet("Teams")]
        public string GetTeams()
        {
            var teams = _gameService.GetTournament().Teams;
            return JsonConvert.SerializeObject(teams);
        }

        [HttpPost("Teams")]
        public IActionResult SetTeams(List<Team> teams)
        {
            if (teams == null)
                return BadRequest();

            _gameService.SetTeams(teams);
            return Ok();
        }

        [HttpPost("Team")]
        public IActionResult AddTeam(Team team)
        {
            if (team == null)
                return BadRequest();

            _gameService.AddTeam(team);
            return Ok();
        }

        [HttpGet("Games")]
        public string GetGames()
        {
            var games = _gameService.GetTournament().Games;
            return JsonConvert.SerializeObject(games);
        }

        [HttpPost("Games")]
        public IActionResult SetGames(List<Game> games)
        {
            if (games == null)
                return BadRequest();

            _gameService.SetGames(games);
            return Ok();
        }

        [HttpPost("Game")]
        public IActionResult AddGame(Game game)
        {
            if (game == null)
                return BadRequest();

            _gameService.AddGame(game);
            return Ok();
        }


        [HttpGet("GetTeamStandings")]
        public string GetTeamStandings()
        {
            return JsonConvert.SerializeObject(_gameService.GetTeamStandings());
        }

        [HttpPost("AddExtraPoints")]
        public IActionResult AddExtraPoints(string teamName, int points)
        {
            if (!_gameService.TeamExists(teamName))
                return BadRequest("No team with that name exists");
            _gameService.AddExtraPoints(teamName, points);
            return Ok();
        }

        [HttpPost("ChangeGameForMatch")]
        public IActionResult ChangeGameForMatch(int matchId, string gameName)
        {
            if (matchId == 0 || string.IsNullOrEmpty(gameName) || !_gameService.ChangeGameForMatch(matchId,gameName))
                return BadRequest();
            return Ok();
        }

        [HttpPost("ChangeTeamsForMatch")]
        public IActionResult ChangeTeamsForMatch(int matchId, string team1Name, string team2Name)
        {
            if (_gameService.GetMatch(matchId) == null || string.IsNullOrEmpty(team1Name) || string.IsNullOrEmpty(team2Name))
                return BadRequest();
            _gameService.ChangeTeamsForMatch(matchId, team1Name, team2Name);
            return Ok();
        }
    }
}
