using Microsoft.EntityFrameworkCore;

namespace EDDS_API.Data;

public class DataContext: DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }
    
    // Core DbSets
    
    
    
}