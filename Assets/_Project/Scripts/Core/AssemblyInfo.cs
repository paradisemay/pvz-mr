using System.Runtime.CompilerServices;

// The order book's transitions are internal to Core (only Shift moves visits on); Core tests check them directly.
[assembly: InternalsVisibleTo("Pvz.Core.Tests")]
