namespace Blaster;

using Framework.Codecs;
using MikeNakis.Kit.Collections;
using MikeNakis.Kit.Extensions;
using Structured;
using static MikeNakis.Kit.GlobalStatics;

sealed class Diary
{
	readonly Dictionary<Stock.Id, Entry> entries = new();

	public Diary()
	{
	}

	public Sys.DateTime TryGetTimestamp( Stock.Id stockId )
	{
		Entry? entry = entries.TryGet( stockId );
		if( entry == null )
			return Sys.DateTime.MinValue;
		Assert( entry.Identifier == stockId );
		entry.Used = true;
		return entry.Timestamp;
	}

	public void SetTimestamp( Stock.Id stockId, Sys.DateTime timestamp )
	{
		Entry entry = new Entry( stockId, timestamp );
		entry.Used = true;
		entries.AddOrReplace( stockId, entry );
	}

	public void Serialize( ItemWriter elementWriter )
	{
		IReadOnlyList<Entry> entriesToSerialize = entries.Values.Where( entry => entry.Used ).Collect().Sorted().AsReadOnlyList;
		elementWriter.WriteArray( "entry", entriesToSerialize, ( entry, elementWriter ) => entry.Save( elementWriter ) );
	}

	public void Deserialize( ItemReader elementReader )
	{
		Assert( entries.IsEmpty() );
		elementReader.ReadArrayInPlace( "entry", elementReader =>
		{
			Entry entry = Entry.Load( elementReader );
			entries.Add( entry.Identifier, entry );
		} );
	}

	sealed class Entry : Sys.IComparable<Entry>
	{
		public static Entry Load( ItemReader itemReader )
		{
			return itemReader.ReadNonNullableObject( objectReader =>
			{
				Stock.Id identifier = objectReader.ReadMember( "assembly", elementReader => elementReader.ReadNonNullableValue( Stock.Id.Codec ) );
				Sys.DateTime lastSuccessfulTestRunTimestamp = objectReader.ReadMember( "timestamp", elementReader => elementReader.ReadNonNullableValue( DateTimeCodec.Instance ) );
				return new Entry( identifier, lastSuccessfulTestRunTimestamp );
			} );
		}

		public Stock.Id Identifier { get; }
		public Sys.DateTime Timestamp { get; }
		public bool Used { get; set; } //TODO: perhaps get rid of this?

		public Entry( Stock.Id stockId, Sys.DateTime timestamp )
		{
			Identifier = stockId;
			Timestamp = timestamp;
			Used = false;
		}

		public void Save( ItemWriter itemWriter )
		{
			itemWriter.WriteObjectInPlace( objectWriter =>
			{
				objectWriter.WriteMember( "assembly", elementWriter => elementWriter.WriteNonNullableValue( Identifier, Stock.Id.Codec ) );
				objectWriter.WriteMember( "timestamp", elementWriter => elementWriter.WriteNonNullableValue( Timestamp, DateTimeCodec.Instance ) );
			} );
		}

		public int CompareTo( Entry? other ) => other == null ? 1 : Identifier.CompareTo( other.Identifier );
	}
}
