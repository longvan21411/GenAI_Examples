#download models
wget https://huggingface.co/rocca/openai-clip-js/tree/main

#command to index the images
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000008.jpg" "This is a normal cat"
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000011.jpg" "This is a normal Muop cat"
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000016.jpg" "This is a white-snowing cat"
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000056.jpg" "This is a blind cat"
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000076.jpg" "This is a gray cat"
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000096.jpg" "This is a black cat"
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000123.jpg" "This is a yellow cat"
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000152.jpg" "This is a white-black cat"
index "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000165.jpg" "This is a white-gray cat"

#search by text
search "This is a white cat"
search "This is a blind cat"
search "This is a gray cat"

#search by image
search "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000016.jpg"
search "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000011.jpg"
search "C:\Users\TVV2HC\source\repos\Github\Img\cat\afhq\val\cat\flickr_cat_000056.jpg"

#search by CLIP with image embedding - not OK
search "C:\\Users\\TVV2HC\\source\\repos\\Github\\GenAI_Examples\\ImageEmbedding_CSharp_OpenSource\\bin\\Debug\\net9.0\\imgs\\cat_CLIP\\flickr_cat_000016.jpg"