<?xml version="1.0" encoding="utf-8" ?> 
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
  <xsl:output method="text"/>
  <xsl:template match="/">
public enum UIStringNames {
    <xsl:apply-templates select="root/data"/>
}    
  </xsl:template>
  <xsl:template match="data"><xsl:text>  </xsl:text><xsl:value-of select="@name"/>, // <xsl:value-of select="value"/><xsl:text>
</xsl:text></xsl:template>
</xsl:stylesheet>