using FunktsioonideUurimine.Mudelid;
using Microsoft.EntityFrameworkCore;

namespace FunktsioonideUurimine.Andmed;

public class FunktsioonideKontekst(DbContextOptions<FunktsioonideKontekst> valikud) : DbContext(valikud)
{
    public DbSet<FunktsiooniUurimine> FunktsiooniUurimised => Set<FunktsiooniUurimine>();

    protected override void OnModelCreating(ModelBuilder mudel)
    {
        mudel.Entity<FunktsiooniUurimine>(olem =>
        {
            olem.ToTable("FunktsiooniUurimised");
            olem.HasIndex(f => f.LuodudAeg);
            olem.HasIndex(f => f.Liik);
        });
    }
}
