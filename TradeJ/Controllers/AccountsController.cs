using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAll()
    {
        var accounts = await db.Accounts
            .OrderBy(a => a.Name)
            .Select(a => new AccountDto(
                a.Id, a.Name, a.Broker, a.AccountNumber, a.Currency,
                a.InitialBalance, a.IsActive, a.CreatedAt, a.Trades.Count,
                a.MT5Server, a.MT5InvestorPassword != null,
                a.MetaApiAccountId, a.MetaApiToken != null, a.MetaApiRegion))
            .ToListAsync();
        return Ok(accounts);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AccountDto>> GetById(int id)
    {
        var a = await db.Accounts
            .Where(a => a.Id == id)
            .Select(a => new AccountDto(
                a.Id, a.Name, a.Broker, a.AccountNumber, a.Currency,
                a.InitialBalance, a.IsActive, a.CreatedAt, a.Trades.Count,
                a.MT5Server, a.MT5InvestorPassword != null,
                a.MetaApiAccountId, a.MetaApiToken != null, a.MetaApiRegion))
            .FirstOrDefaultAsync();
        return a is null ? NotFound() : Ok(a);
    }

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create([FromBody] CreateAccountDto dto)
    {
        var account = new Account
        {
            Name                 = dto.Name,
            Broker               = dto.Broker,
            AccountNumber        = dto.AccountNumber,
            Currency             = dto.Currency,
            InitialBalance       = dto.InitialBalance,
            MT5Server            = dto.MT5Server,
            MT5InvestorPassword  = dto.MT5InvestorPassword,
            MetaApiAccountId     = dto.MetaApiAccountId,
            MetaApiToken         = dto.MetaApiToken,
            MetaApiRegion        = dto.MetaApiRegion
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var result = new AccountDto(account.Id, account.Name, account.Broker,
            account.AccountNumber, account.Currency, account.InitialBalance, account.IsActive, account.CreatedAt, 0,
            account.MT5Server, account.MT5InvestorPassword != null,
            account.MetaApiAccountId, account.MetaApiToken != null, account.MetaApiRegion);
        return CreatedAtAction(nameof(GetById), new { id = account.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAccountDto dto)
    {
        var account = await db.Accounts.FindAsync(id);
        if (account is null) return NotFound();

        account.Name          = dto.Name;
        account.AccountNumber = dto.AccountNumber;
        account.Currency      = dto.Currency;
        account.InitialBalance = dto.InitialBalance;
        account.IsActive      = dto.IsActive;
        account.MT5Server     = dto.MT5Server?.Length > 0 ? dto.MT5Server : null;
        account.MetaApiRegion = dto.MetaApiRegion;

        // MT5InvestorPassword: null = keep existing, empty = clear, value = update
        if (dto.MT5InvestorPassword is not null)
            account.MT5InvestorPassword = dto.MT5InvestorPassword.Length > 0 ? dto.MT5InvestorPassword : null;

        // Only overwrite token when a new value is provided (empty string = keep existing, null = clear)
        if (dto.MetaApiToken is not null)
            account.MetaApiToken = dto.MetaApiToken.Length > 0 ? dto.MetaApiToken : null;

        // MetaApiAccountId: null means "leave unchanged", empty string means "clear"
        if (dto.MetaApiAccountId is not null)
            account.MetaApiAccountId = dto.MetaApiAccountId.Length > 0 ? dto.MetaApiAccountId : null;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var account = await db.Accounts.Include(a => a.Trades).FirstOrDefaultAsync(a => a.Id == id);
        if (account is null) return NotFound();
        if (account.Trades.Count > 0)
            return BadRequest(new { message = "Cannot delete account with existing trades. Remove trades first." });

        db.Accounts.Remove(account);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
