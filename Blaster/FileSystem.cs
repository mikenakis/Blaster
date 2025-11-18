namespace Blaster;

using System.Collections.Generic;

public abstract class FileSystem
{
	public abstract FileItem CreateItem( FileName fileName );
	public abstract void Delete( FileName fileName );

	public FileItem CopyFrom( FileItem sourceItem )
	{
		FileItem targetItem = CreateItem( sourceItem.FileName );
		targetItem.CopyFrom( sourceItem );
		return targetItem;
	}

	public abstract IEnumerable<FileItem> EnumerateItems();

	public IEnumerable<FileItem> EnumerateItems( DirectoryName directoryName )
	{
		foreach( FileItem item in EnumerateItems() )
		{
			if( item.FileName.DirectoryName == directoryName )
				yield return item;
		}
	}

	public abstract bool Exists( FileName fileName );

	public void Clear()
	{
		foreach( FileItem? item in EnumerateItems() )
			item.Delete();
	}
}
