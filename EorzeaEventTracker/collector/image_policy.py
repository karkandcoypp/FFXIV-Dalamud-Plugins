"""Official image URL contract shared by enrichment and feed tests (see FeedPolicy.cs)."""
from urllib.parse import urlparse

IMAGE_HOSTS = frozenset(('lds-img.finalfantasyxiv.com', 'img.finalfantasyxiv.com'))


def is_image_url(value):
    if not isinstance(value, str) or not value or len(value) > 2048:
        return False
    try:
        uri = urlparse(value)
        return (uri.scheme == 'https' and uri.hostname in IMAGE_HOSTS
                and uri.port in (None, 443) and not uri.username and not uri.password)
    except ValueError:
        return False
