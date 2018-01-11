﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using Action.Models;
using Action.Models.Watson;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Internal.System.Collections.Sequences;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Action.Extensions;
using Action.VewModels;
using Action.Models.Plans;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Configuration;
using Action.Services.SMTP;

namespace Action.Controllers
{
	[Route("api/[controller]")]
	[EnableCors("Default")]
	public class AccountController : Controller
	{
		private readonly ApplicationDbContext _dbContext;
		private readonly HtmlEncoder _htmlEncoder;
		private readonly UserManager<User> _userManager;
		private readonly IConfigurationRoot _configurationRoot;

		public AccountController(UserManager<User> userManager, IConfigurationRoot configurationRoot, HtmlEncoder htmlEncoder, ApplicationDbContext dbContext = null)
		{
			_userManager = userManager;
			_configurationRoot = configurationRoot;
			_dbContext = dbContext;
			_htmlEncoder = htmlEncoder;
		}

		// GET: api/values
		[HttpGet("{id}")]
		public dynamic Get([FromRoute] int id)
		{
			try
			{
				if (_dbContext == null)
					return NotFound("No database connection");
				var data = _dbContext.Accounts.Include(x => x.Entities).Include(x => x.Administrator).Include(x => x.Plan).ThenInclude(x => x.Features).Include(x => x.Users).FirstOrDefault(x => x.Id == id);

				if (data == null)
					return StatusCode((int)EServerError.BusinessError, new List<string> { "Object not found with ID " + id.ToString() + "." });

				AccountViewModel lAccountViewModel = new AccountViewModel();
				lAccountViewModel.Id = data.Id;
				lAccountViewModel.Status = data.Status.ToString();
				lAccountViewModel.Plan = new PlanViewModel
				{
					Id = data.Plan.Id,
					Name = data.Plan.Name,
					Price = data.Plan.Price,
					Slug = data.Plan.Slug,
				};
				data.Plan.Features.ForEach(x => lAccountViewModel.Plan.Features.Add(new FeatureViewModel
				{
					Description = x.Description
				}));

				data.SecondaryPlans.ForEach(x => lAccountViewModel.SecondaryPlans.Add(new SecondaryPlanViewModel
				{
					Account = lAccountViewModel,
					Id = x.Id,
					AllowedUsers = x.AllowedUsers,
					Name = x.Name,
					Price = x.Price,
					StartDate = x.StartDate
				}));

				data.Entities.ForEach(x => lAccountViewModel.Entities.Add(new EntityViewModel
				{
					Entity = x.Name,
					Id = x.Id,
					ImageUrl = x.PictureUrl,
					Type = x.Category
				}));

				data.Users.ForEach(x => lAccountViewModel.Users.Add(new UserViewModel
				{
					AccountId = x.AccountId.Value,
					Email = x.Email,
					Id = x.Id.ToString(),
					Name = x.Name,
					Phone = x.PhoneNumber,
					Role = x.Account.Administrator != null ? "ADMIN" : "USER",
					SurName = x.Surname,
				}));

				return Ok(lAccountViewModel);
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}

		[HttpPost("{id}/entities")]
		public dynamic Post([FromRoute] int id, [FromBody] EntityViewModel model)
		{
			if (ModelState.IsValid)
			{
				try
				{
					var account = _dbContext.Accounts.Include(x => x.Entities).FirstOrDefault(y => y.Id == id);
					if (account == null)
						throw new Exception("Account not found with ID: " + id);

					if (model.Id == 0)
					{
						var obj = new Entity
						{
							Name = model.Entity,
							CategoryId = Enum.Parse<ECategory>(model.Type),
							Date = DateTime.Today,
							//Alias = model.Alias,
							//FacebookUser = model.FacebookUser,
							//InstagranUser = model.InstagranUser,
							PictureUrl = model.ImageUrl,
							//SiteUrl = model.SiteUrl,
							//TweeterUser = model.TweeterUser,
							//YoutubeUser = model.YoutuberUser
						};
						_dbContext.Entities.Add(obj);
						account.Entities.Add(obj);
					}
					else
					{
						if (account.Entities == null)
							account.Entities = new List<Entity>();

						if (_dbContext.Entities.Find((long)model.Id) == null)
							throw new Exception("Entity not found with ID: " + model.Id);

						if (account.Entities.All(x => x.Id != model.Id))
							account.Entities.Add(_dbContext.Entities.Find((long)model.Id));
					}

					_dbContext.Entry(account).State = EntityState.Modified;
					_dbContext.SaveChanges();
					return account.Entities;
				}
				catch (Exception ex)
				{
					return StatusCode((int)EServerError.BusinessError, new List<string> { ex.Message });
				}
			}
			else
			{
				return StatusCode((int)EServerError.ModelIsNotValid, ModelState.GetErrors());
			}
		}

		//TODO: Implementar o Upload de Imagem
		[HttpPost("{id}/image")]
		public dynamic Post([FromRoute] int id, [FromBody] string image)
		{
			return Ok();// "https://cdn1.iconfinder.com/data/icons/social-messaging-productivity-1-1/128/gender-male2-512.png";
		}

		[HttpPost("{id}/users")]
		public async Task<IActionResult> PostUsers([FromRoute] int id, [FromBody] RequireAuthViewModel requireAuthView)
		{
			try
			{
				if (requireAuthView.Email == null || requireAuthView.Email.Equals(""))
					return StatusCode((int)EServerError.BusinessError, new List<string> { "E-mail não informado." });
				if (requireAuthView.Url == null || requireAuthView.Url.Equals(""))
					return StatusCode((int)EServerError.BusinessError, new List<string> { "URL não informado." });

				if (_dbContext == null)
					return NotFound("No database connection");
				var data = _dbContext.Accounts.Include(x => x.Users).FirstOrDefault(x => x.Id.Equals(id));

				if (data == null)
					return StatusCode((int)EServerError.BusinessError, new List<string> { "Account not found with ID " + id.ToString() + "." });

				var user = data.Users.FirstOrDefault(x => x.Email.Equals(requireAuthView.Email));
				if(user != null)
					return StatusCode((int)EServerError.BusinessError, new List<string> { "Usuário já cadastrado com este e-mail para esta Conta." });

				user = new User
				{
					UserName = requireAuthView.Email,
					Email = requireAuthView.Email,
					Name = "",
					Surname = "",
					PhoneNumber = "",
					PlanId = data.PlanId,
					AccountId = data.Id
				};
				data.Users.Add(user);
				_dbContext.Entry(data).State = EntityState.Modified;
				_dbContext.SaveChanges();

				var userClaims = await _userManager.GetClaimsAsync(user);
				var plan = _dbContext.Accounts.Find(user.AccountId);
				var claims = new[]
					{
						new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
						new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
						new Claim(JwtRegisteredClaimNames.Email, user.Email),
						new Claim("accountId", user.AccountId.ToString()),
						new Claim("planId", plan.Id.ToString())
					}.Union(userClaims);
				var symmetricSecurityKey =
						new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configurationRoot["JwtSecurityToken:Key"]));
				var signingCredentials =
					new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

				var jwtSecurityToken = new JwtSecurityToken(
					_configurationRoot["JwtSecurityToken:Issuer"],
					_configurationRoot["JwtSecurityToken:Audience"],
					claims,
					expires: DateTime.UtcNow.AddDays(1),
					signingCredentials: signingCredentials
				);
				var token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);

				SmtpService.SendMessage(user.Email, "[ACTION-API Acesso]",
						string.Concat(requireAuthView.Url, "?token=", token));

				return Ok(new
				{
					email = user.Email,
					url = requireAuthView.Url
				});

			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}

		//TODO Adicionado delete
		[HttpDelete("{idaccount}/users/{id}")]
		public dynamic Delete([FromRoute] int idaccount, [FromRoute] string id)
		{
			try
			{
				if (_dbContext == null)
					return NotFound("No database connection");
				var data = _dbContext.Accounts.Include(x => x.Users).FirstOrDefault(x => x.Id == idaccount);

				if (data == null)
					return StatusCode((int)EServerError.BusinessError, new List<string> { "Account not found with ID " + idaccount.ToString() + "." });

				var user = data.Users.FirstOrDefault(x => x.Id.Equals(id));

				if (user == null)
					return StatusCode((int)EServerError.BusinessError, new List<string> { "User not found with ID " + id.ToString() + "." });

				if (user.Account != null && user.Account.Administrator == user)
					return StatusCode((int)EServerError.BusinessError, new List<string> { "Usuário Administrador não pode ser excluído." });

				data.Users.Remove(user);
				_dbContext.Entry(data).State = EntityState.Modified;
				_dbContext.SaveChanges();

				return Ok();
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}

		//TODO: Adicionado SecondaryPlan
		[HttpPost("{id}/secondaryplans")]
		public dynamic PostSecondaryPlans([FromRoute] int id, [FromBody] SecondaryPlanViewModel secondaryPlanView)
		{
			try
			{
				var account = _dbContext.Accounts.Include(x => x.SecondaryPlans).FirstOrDefault(y => y.Id == id);
				if (account == null)
					throw new Exception("Account not found with ID: " + id);

				var secondaryPlan = account.SecondaryPlans.FirstOrDefault(x => x.Id == secondaryPlanView.Id);
				if (secondaryPlan != null)
					throw new Exception("SecondaryPlan already associated with the Account.");

				SecondaryPlan newSecondaryPlan = new SecondaryPlan
				{
					Account = account,
					AccountId = account.Id,
					AllowedUsers = secondaryPlanView.AllowedUsers,
					Name = secondaryPlanView.Name,
					Price = secondaryPlanView.Price,
					StartDate = secondaryPlanView.StartDate
				};

				account.SecondaryPlans.Add(newSecondaryPlan);
				_dbContext.Entry(account).State = EntityState.Modified;
				_dbContext.SaveChanges();

				return Ok(newSecondaryPlan);
			}
			catch (Exception ex)
			{
				return StatusCode((int)EServerError.BusinessError, new List<string> { ex.Message });
			}
		}

		[HttpPost("cancela/{id}")]
		public dynamic PostCancela([FromRoute] int id)
		{
			return Ok(new
			{
				highPrice = true,
				platformNotUseful = true,
				doesntMatchMyNeeds = true,
				unableToUseThePlatformEffectively = true,
				message = "message message message message message "
			});
		}

		[HttpGet("{id}/payments")]
		public dynamic GetPayments([FromRoute] int id)
		{
			return Ok(new[]
			{
				new
				{
					id= 123456,
					plan=new
					{
						id= 2,
						name= "MARCA",
						slug= "marca",
						features= new[]
						{
							new { description= "Monitore a relevância digital da sua marca ou de seu produto"},
							new { description= "Cadastre até 3 usuários para o monitoramento"},
							new { description= "Encontre as celebridades perfeitas para as suas campanhas"}
						},
						price= 2000
						},
						secondaryPlans= new []
						{
							new
							{
								id= "123456",
								allowedUsers= 3,
								name= "3 usuários adicionais",
								price= "12345",
								startDate= "2017-11-06T00:00:00"
							}
						},
						expirationdate= "2017-11-06T00:00:00",
						status= "PENDING" // PENDING | PAID
				}
			});
		}
	}

	public class AddEntityViewModel
	{
		//TODO: Adicionado as Propriedades imageUrl e industryId
		public int Id { get; set; }
		[Required(ErrorMessage = "Nome não informado.")]
		public string Name { get; set; }
		public string Alias { get; set; }
		public int CategoryId { get; set; }
		[Required(ErrorMessage = "Categoria não informada.")]
		public string Category { get; set; }
		public string Date { get; set; }
		public string FacebookUser { get; set; }
		public string TweeterUser { get; set; }
		public string InstagranUser { get; set; }
		public string YoutuberUser { get; set; }
		public string PictureUrl { get; set; }
		public string SiteUrl { get; set; }
	}
}